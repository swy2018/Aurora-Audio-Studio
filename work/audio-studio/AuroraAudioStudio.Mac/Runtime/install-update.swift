import Foundation
import Darwin

// Keep the old bundle only until replacement validates; never touch model/project storage.
// https://developer.apple.com/documentation/foundation/filemanager/moveitem(at:to:)
let manager = FileManager.default
func fail(_ message: String) -> NSError { NSError(domain: "AuroraUpdate", code: 1, userInfo: [NSLocalizedDescriptionKey: message]) }
@discardableResult
func run(_ executable: String, _ arguments: [String]) throws -> String {
    let process = Process(), pipe = Pipe()
    process.executableURL = URL(fileURLWithPath: executable)
    process.arguments = arguments
    process.standardOutput = pipe; process.standardError = pipe
    try process.run()
    let data = pipe.fileHandleForReading.readDataToEndOfFile()
    process.waitUntilExit()
    let output = String(data: data, encoding: .utf8) ?? ""
    if process.terminationStatus != 0 { throw fail("\(URL(fileURLWithPath: executable).lastPathComponent): \(output)") }
    return output
}
func verify(_ app: URL) throws -> Int {
    let requirement = "anchor apple generic and identifier \"com.suwan.aurora.mac\" and certificate leaf[subject.OU] = \"V9JR96Z9YW\""
    try run("/usr/bin/codesign", ["--verify", "--deep", "--strict", "-R", "=" + requirement, app.path])
    try run("/usr/sbin/spctl", ["--assess", "--type", "execute", app.path])
    guard let info = NSDictionary(contentsOf: app.appendingPathComponent("Contents/Info.plist")),
          let value = info["CFBundleVersion"] as? String, let build = Int(value) else { throw fail("应用版本信息无效。") }
    return build
}
func status(_ url: URL, _ value: [String: String]) throws {
    try JSONSerialization.data(withJSONObject: value).write(to: url, options: .atomic)
}

@discardableResult
func replaceBundle(staged: URL, target: URL, backup: URL, validate: (URL) throws -> Void) throws -> String? {
    try manager.moveItem(at: target, to: backup)
    do {
        try manager.moveItem(at: staged, to: target)
        try validate(target)
    } catch {
        if manager.fileExists(atPath: target.path) {
            try manager.moveItem(at: target, to: staged.deletingLastPathComponent().appendingPathComponent("failed.app"))
        }
        try manager.moveItem(at: backup, to: target)
        throw error
    }
    do {
        try manager.removeItem(at: backup)
        return nil
    } catch {
        return "应用已安装，但临时旧应用未能清理：\(backup.path)。\(error.localizedDescription)"
    }
}

func stableBuild(_ version: String) throws -> Int {
    guard version.range(of: #"^\d+\.\d+\.\d+$"#, options: .regularExpression) != nil else {
        throw fail("回退目标必须是正式版。")
    }
    let parts = version.split(separator: ".").compactMap { Int($0) }
    guard parts.count == 3, parts[0] < 1000, parts[1] < 100, parts[2] < 100 else {
        throw fail("回退版本号无效。")
    }
    return parts[0] * 1000000 + parts[1] * 10000 + parts[2] * 100 + 99
}

#if UPDATE_REPLACEMENT_TESTS
let fixtureRoot = URL(fileURLWithPath: CommandLine.arguments[1]).appendingPathComponent(UUID().uuidString)
try manager.createDirectory(at: fixtureRoot, withIntermediateDirectories: true)
for failure in [false, true] {
    let root = fixtureRoot.appendingPathComponent(failure ? "rollback" : "success")
    try manager.createDirectory(at: root, withIntermediateDirectories: true)
    let old = root.appendingPathComponent("current.app"), staged = root.appendingPathComponent("staged.app"), backup = root.appendingPathComponent("backup.app")
    try Data("old".utf8).write(to: old); try Data("new".utf8).write(to: staged)
    do {
        try replaceBundle(staged: staged, target: old, backup: backup) { _ in if failure { throw fail("fixture validation failure") } }
        assert(!failure)
    } catch { assert(failure) }
    let actual = try String(contentsOf: old, encoding: .utf8)
    assert(actual == (failure ? "old" : "new"))
    assert(!manager.fileExists(atPath: backup.path))
    if failure {
        let rejected = try String(contentsOf: root.appendingPathComponent("failed.app"), encoding: .utf8)
        assert(rejected == "new")
    }
    print("PASS: \(failure ? "failed replacement restores old app" : "successful replacement removes temporary old app")")
}
print("Replacement test files retained: \(fixtureRoot.path)")
#else

var report: URL?
var originalApp: URL?
var originalPid: Int32?
do {
    let args = CommandLine.arguments
    if args.count == 3 && args[1] == "--verify" {
        print("Verified notarized Aurora build \(try verify(URL(fileURLWithPath: args[2])))")
        exit(0)
    }
    guard args.count == 5 || (args.count == 7 && args[5] == "--rollback-to"),
          let pid = Int32(args[3]), pid > 1 else { throw fail("更新参数无效。") }
    var rollbackBuild: Int? = nil
    if args.count == 7 { rollbackBuild = try stableBuild(args[6]) }
    let dmg = URL(fileURLWithPath: args[1]).standardizedFileURL
    let target = URL(fileURLWithPath: args[2]).standardizedFileURL
    let result = URL(fileURLWithPath: args[4]).standardizedFileURL
    report = result
    originalApp = target; originalPid = pid
    guard target.pathExtension == "app", manager.fileExists(atPath: dmg.path), dmg.pathExtension == "dmg",
          target.resolvingSymlinksInPath() == target else { throw fail("安装目标或安装包路径无效。") }
    let oldBuild = try verify(target)
    let identity = UUID().uuidString
    let parent = target.deletingLastPathComponent()
    let transaction = parent.appendingPathComponent(".Aurora-updates/\(identity)", isDirectory: true)
    try manager.createDirectory(at: transaction, withIntermediateDirectories: true)
    let staged = transaction.appendingPathComponent(target.lastPathComponent)
    let backup = transaction.appendingPathComponent("previous.app")
    let mount = dmg.deletingLastPathComponent().appendingPathComponent("mount-\(identity)", isDirectory: true)
    try manager.createDirectory(at: mount, withIntermediateDirectories: true)
    try run("/usr/bin/hdiutil", ["attach", "-readonly", "-nobrowse", "-mountpoint", mount.path, dmg.path])
    do {
        let source = mount.appendingPathComponent("Aurora Audio Studio.app")
        let sourceBuild = try verify(source)
        if let rollbackBuild {
            guard sourceBuild == rollbackBuild && sourceBuild < oldBuild else { throw fail("安装包与所选正式版不匹配。") }
        } else {
            guard sourceBuild > oldBuild else { throw fail("安装包不是更新的 Aurora 版本。") }
        }
        try run("/usr/bin/ditto", [source.path, staged.path])
        _ = try verify(staged)
    } catch {
        _ = try? run("/usr/bin/hdiutil", ["detach", mount.path])
        throw error
    }
    try run("/usr/bin/hdiutil", ["detach", mount.path])
    try manager.createDirectory(at: backup.deletingLastPathComponent(), withIntermediateDirectories: true)
    try status(result, ["state": "ready"])
    let deadline = Date().addingTimeInterval(120)
    while kill(pid, 0) == 0 {
        guard Date() < deadline else { throw fail("当前 Aurora 未退出，更新已停止，旧版保持不变。") }
        Thread.sleep(forTimeInterval: 0.25)
    }
    // Recheck after waiting; do not overwrite an app changed by another installer.
    guard try verify(target) == oldBuild else { throw fail("安装期间应用发生变化，已停止替换。") }
    let cleanupWarning = try replaceBundle(staged: staged, target: target, backup: backup) { _ = try verify($0) }
    var installedStatus = ["state": "installed"]
    if let cleanupWarning { installedStatus["cleanupWarning"] = cleanupWarning }
    try status(result, installedStatus)
    try run("/usr/bin/open", [target.path])
} catch {
    if let report { try? status(report, ["state": "failed", "message": error.localizedDescription]) }
    if let originalApp, let originalPid, kill(originalPid, 0) != 0, manager.fileExists(atPath: originalApp.path) {
        _ = try? run("/usr/bin/open", [originalApp.path])
    }
    fputs("\(error.localizedDescription)\n", stderr)
    exit(1)
}
#endif
