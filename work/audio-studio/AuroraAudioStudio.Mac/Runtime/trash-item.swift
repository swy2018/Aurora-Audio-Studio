import Foundation
if CommandLine.arguments.count != 2 { exit(2) }
do {
    let source = URL(fileURLWithPath: CommandLine.arguments[1]).standardizedFileURL
    var destination: NSURL?
    try FileManager.default.trashItem(at: source, resultingItemURL: &destination)
    let result = ["kind": "trashed", "path": destination?.path ?? ""]
    print(String(data: try JSONSerialization.data(withJSONObject: result), encoding: .utf8)!)
} catch { fputs("\(error)\n", stderr); exit(1) }
