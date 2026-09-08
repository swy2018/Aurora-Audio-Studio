import Foundation
import AVFoundation
import AudioToolbox

// Offline MIDI audition with the macOS built-in General MIDI sound bank.
// The original MIDI is never modified.
guard CommandLine.arguments.count == 3 else { exit(2) }
do {
    let engine = AVAudioEngine()
    let sampler = AVAudioUnitSampler()
    engine.attach(sampler)
    let format = AVAudioFormat(standardFormatWithSampleRate: 44100, channels: 2)!
    engine.connect(sampler, to: engine.mainMixerNode, format: format)
    let bank = URL(fileURLWithPath: "/System/Library/Components/CoreAudio.component/Contents/Resources/gs_instruments.dls")
    try sampler.loadSoundBankInstrument(at: bank, program: 0, bankMSB: UInt8(kAUSampler_DefaultMelodicBankMSB), bankLSB: 0)
    let sequence = AVAudioSequencer(audioEngine: engine)
    try sequence.load(from: URL(fileURLWithPath: CommandLine.arguments[1]), options: [])
    for track in sequence.tracks { track.destinationAudioUnit = sampler }
    let duration = (sequence.tracks.map { $0.lengthInSeconds }.max() ?? 0) + 2
    guard duration > 2, duration < 3600 else { throw NSError(domain: "Aurora", code: 1, userInfo: [NSLocalizedDescriptionKey: "MIDI duration is invalid or exceeds one hour."]) }
    try engine.enableManualRenderingMode(.offline, format: format, maximumFrameCount: 4096)
    try engine.start()
    sequence.prepareToPlay()
    try sequence.start()
    let audio = try AVAudioFile(forWriting: URL(fileURLWithPath: CommandLine.arguments[2]), settings: format.settings)
    let buffer = AVAudioPCMBuffer(pcmFormat: format, frameCapacity: 4096)!
    let end = AVAudioFramePosition(duration * format.sampleRate)
    var retries = 0
    while engine.manualRenderingSampleTime < end {
        let count = AVAudioFrameCount(min(4096, end - engine.manualRenderingSampleTime))
        switch try engine.renderOffline(count, to: buffer) {
        case .success: try audio.write(from: buffer); retries = 0
        case .cannotDoInCurrentContext: retries += 1
        case .insufficientDataFromInputNode: retries += 1
        case .error: throw NSError(domain: "Aurora", code: 2)
        @unknown default: throw NSError(domain: "Aurora", code: 3)
        }
        if retries > 100 { throw NSError(domain: "Aurora", code: 4) }
    }
    sequence.stop()
    engine.stop()
} catch {
    FileHandle.standardError.write(Data(error.localizedDescription.utf8))
    exit(1)
}
