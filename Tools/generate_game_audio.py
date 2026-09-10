"""Rebuild the game's original synthesized audio (Python + NumPy; no external samples)."""
from pathlib import Path
import argparse
import uuid
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/Resources/Audio'
OUT.mkdir(parents=True, exist_ok=True)
RATE = 44100
RNG = np.random.default_rng(7162)


def write(name, signal, peak=.8, music=False, sample_rate=RATE):
    signal = signal * (peak / max(float(np.max(np.abs(signal))), 1e-9))
    pcm = np.round(signal * 32767).astype('<i2')
    with wave.open(str(OUT / (name + '.wav')), 'wb') as wav:
        wav.setnchannels(1 if signal.ndim == 1 else signal.shape[1])
        wav.setsampwidth(2)
        wav.setframerate(sample_rate)
        wav.writeframes(pcm.tobytes())
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'sand-game/audio/' + name).hex
    meta = OUT / (name + '.wav.meta')
    if not meta.exists():
        meta.write_text(f'''fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: {1 if music else 0}
    quality: 0.8
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: {0 if music else 1}
  normalize: 0
  ambisonic: 0
  loadInBackground: 0
  userData:
  assetBundleName:
  assetBundleVariant:
''', encoding='utf-8')
    print(f'{name}: {len(signal) / sample_rate:.2f}s, peak {np.max(np.abs(signal)):.3f}')


def mallet(note, duration=1.5, soft=False):
    t = np.arange(int(duration * RATE)) / RATE
    frequency = 440 * 2 ** ((note - 69) / 12)
    attack = 1 - np.exp(-t / (.018 if soft else .003))
    signal = np.sin(2 * np.pi * frequency * t) * np.exp(-t / (.65 if soft else .28))
    signal += .22 * np.sin(2 * np.pi * frequency * 2.002 * t) * np.exp(-t / .12)
    signal += .08 * np.sin(2 * np.pi * frequency * 3 * t) * np.exp(-t / .065)
    return signal * attack * np.minimum(1, (duration - t) / .05)


def sand_pour():
    """Airy fine sand, synthesized from the reference's broad spectral balance."""
    rng = np.random.default_rng(10929)
    length = RATE * 6
    frequency = np.fft.rfftfreq(length, 1 / RATE)

    def movement(low, high, depth):
        shape = (1 - np.exp(-(frequency / low) ** 4)) * np.exp(-(frequency / high) ** 4)
        envelope = np.fft.irfft(np.fft.rfft(rng.normal(size=length)) * shape, n=length)
        envelope /= max(np.sqrt(np.mean(envelope ** 2)), 1e-9)
        return 1 + depth * np.tanh(envelope / 2)

    # Broad-band measurements of the steady pour at 1.4-4.0 s in the user's
    # reference: https://www.youtube.com/shorts/zfWO4ViRfbQ (ASMR PLUSH).
    # These are spectral design points, not samples from the recording. The
    # waveform is fresh seeded noise; generation needs no recording or network.
    # Most reference energy is above 4 kHz, especially 8-15 kHz. Earlier low
    # passes removed that fine airy texture and left a coarse midrange rustle.
    profile_hz = np.array([
        20, 40, 80, 125, 200, 315, 500, 800, 1000, 1250, 1600, 2000,
        2500, 3150, 4000, 5000, 6300, 8000, 10000, 12500, 15000,
        17500, 20000, 21000, 22050,
    ])
    profile_db = np.array([
        -90, -72, -52.2, -52.4, -41.3, -29.1, -23.7, -16.4, -15.3,
        -15.6, -12.4, -10.8, -9.9, -8.5, -5.6, -4.3, -2.9, 0, -.3,
        -1.0, -2.2, -7.8, -11.5, -45, -96,
    ])
    profile = 10 ** (np.interp(np.log(np.maximum(frequency, 20)),
                               np.log(profile_hz), profile_db) / 20)
    profile[0] = 0
    rustle = np.fft.irfft(np.fft.rfft(rng.normal(size=length)) * profile, n=length)

    # Match the reference's restrained flow variation, without discrete grain
    # impacts or its whole-shot fade. GameAudio handles the start and release.
    rustle *= movement(.8, 8, .17) * movement(12, 38, .045)
    rustle -= np.mean(rustle)
    rustle *= .022 / np.sqrt(np.mean(rustle ** 2))
    return rustle


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--sand-only', action='store_true', help='Regenerate only the pouring-sand loop.')
args = parser.parse_args()
if args.sand_only:
    rustle = sand_pour()
    write('SandPour', rustle, min(.72, float(np.max(np.abs(rustle)))))
    raise SystemExit(0)


# Hollow pail tap: a short noise contact followed by two damped resonances.
t = np.arange(int(.17 * RATE)) / RATE
tap = (.64 * np.sin(2 * np.pi * 540 * t) + .25 * np.sin(2 * np.pi * 1230 * t)) * np.exp(-t * 44)
tap += .28 * RNG.normal(size=len(t)) * np.exp(-t * 250)
tap *= np.minimum(1, t / .001) * np.minimum(1, (.17 - t) / .012)
write('BucketTap', tap, .7)

rustle = sand_pour()
write('SandPour', rustle, min(.72, float(np.max(np.abs(rustle)))))


def chime(name, notes, offsets, duration):
    signal = np.zeros(int(duration * RATE))
    for note, offset in zip(notes, offsets):
        sound = mallet(note, .9)
        start = int(offset * RATE)
        count = min(len(sound), len(signal) - start)
        signal[start:start + count] += sound[:count] * .65
    signal *= np.minimum(1, (duration - np.arange(len(signal)) / RATE) / .05)
    write(name, signal, .75)


chime('BucketFilled', [76, 79, 84], [0, .075, .15], .9)
chime('LevelComplete', [72, 76, 79, 84, 88], [0, .1, .2, .33, .33], 1.5)

# Light puzzle-game music. The eight sections change their foreground
# instrument, groove, register and harmonic rhythm, with real rests and
# instrumental handovers. Keep the approved keyboard theme as an anchor.
music_rate = 22050
duration = 180
total_bars = 64
# 64 complete bars at 85 1/3 BPM give exactly three minutes, with a natural
# phrase boundary at the loop. This stays close to the approved 84 BPM feel.
beat = duration / (total_bars * 4)
bpm = 60 / beat
length = round(duration * music_rate)
music = np.zeros((length, 2))
music_rng = np.random.default_rng(9362)


def mix_music(sound, at, gain, pan=0):
    indices = (round(at * music_rate) + np.arange(len(sound))) % length
    music[indices, 0] += sound * gain * np.sqrt((1 - pan) / 2)
    music[indices, 1] += sound * gain * np.sqrt((1 + pan) / 2)


def soft_key(note, seconds=1.8, bass=False):
    time = np.arange(round(seconds * music_rate)) / music_rate
    hz = 440 * 2 ** ((note - 69) / 12)
    phase = 2 * np.pi * hz * time
    # A felt-like attack and progressively shorter upper harmonics give
    # definition without the metallic attack of the original mallets.
    attack = 1 - np.exp(-(time / (.045 if bass else .028)) ** 2)
    release = np.minimum(1, (seconds - time) / .12) ** 2
    tone = np.sin(phase) * np.exp(-time / (.38 if bass else .52))
    tone += .22 * np.sin(2 * phase) * np.exp(-time / .25)
    tone += .045 * np.sin(3 * phase) * np.exp(-time / .13)
    return tone * attack * release


def nylon_pluck(note, seconds=1.8):
    """Damped string modes: a woody pluck with a rounded, low-pass attack."""
    time = np.arange(round(seconds * music_rate)) / music_rate
    hz = 440 * 2 ** ((note - 69) / 12)
    tone = np.zeros(len(time))
    for harmonic in range(1, 10):
        amplitude = np.exp(-(harmonic * hz / 2000) ** 2) / harmonic ** 1.35
        amplitude *= .55 + .45 * abs(np.sin(harmonic * np.pi * .23))
        frequency = hz * harmonic * np.sqrt(1 + .00004 * harmonic * harmonic)
        tone += amplitude * np.sin(2 * np.pi * frequency * time) * np.exp(-time * (1.6 + .8 * harmonic))
    attack = 1 - np.exp(-(time / .012) ** 2)
    release = np.minimum(1, (seconds - time) / .10) ** 2
    tone *= attack * release
    return tone / max(1, np.max(np.abs(tone)))


def warm_reed(note, seconds=1.1):
    """Soft sustained woodwind colour, distinct from both decaying keyboards."""
    time = np.arange(round(seconds * music_rate)) / music_rate
    hz = 440 * 2 ** ((note - 69) / 12)
    phase = 2 * np.pi * hz * time + .10 * np.sin(2 * np.pi * 4.7 * time)
    tone = np.sin(phase) + .16 * np.sin(3 * phase) + .032 * np.sin(5 * phase)
    attack = np.sin(np.minimum(1, time / .12) * np.pi / 2) ** 2
    release = np.sin(np.minimum(1, (seconds - time) / .24) * np.pi / 2) ** 2
    return tone * attack * release * (.88 + .05 * np.sin(2 * np.pi * .6 * time))


def brushed_tap():
    """A very quiet, dark brush stroke, without a cymbal or hard drum transient."""
    time = np.arange(round(.17 * music_rate)) / music_rate
    noise = music_rng.normal(size=len(time))
    frequency = np.fft.rfftfreq(len(noise), 1 / music_rate)
    noise = np.fft.irfft(np.fft.rfft(noise) * np.exp(-(frequency / 1400) ** 2), n=len(noise))
    noise /= max(np.max(np.abs(noise)), 1e-6)
    envelope = (1 - np.exp(-(time / .023) ** 2)) * np.exp(-time / .045)
    envelope *= np.minimum(1, (.17 - time) / .045) ** 2
    return (noise + .28 * np.sin(2 * np.pi * 185 * time)) * envelope


def voice_clip(voice, note, seconds):
    if voice == 'nylon':
        return nylon_pluck(note, seconds)
    if voice == 'reed':
        # A held note needs less amplitude than a short decaying note.
        return warm_reed(note, seconds) * .43
    return soft_key(note, seconds)


chords = {
    'C6': (48, [55, 60, 64, 69]),
    'Am7': (45, [55, 60, 64, 67]),
    'Fmaj7': (41, [53, 57, 60, 64]),
    'G6': (43, [55, 59, 62, 64]),
    'Dm7': (50, [53, 57, 60, 65]),
    'Em7': (40, [55, 59, 62, 67]),
    'C/E': (40, [55, 60, 64, 69]),
    'F6': (41, [53, 57, 60, 62]),
    'Gm7': (43, [55, 58, 62, 65]),
    'Bbmaj7': (46, [53, 57, 62, 65]),
}
# Preserve the recognizable theme from the approved short version as the opening.
opening = [
    [(0.5, 64), (1.5, 67), (3, 69)], [(1, 67), (2.5, 64)],
    [(0.5, 60), (2, 64), (3, 67)], [(1, 64), (2.75, 60)],
    [(0.5, 57), (1.5, 60), (3, 64)], [(1, 67), (2.5, 64)],
    [(0.5, 62), (2, 64), (3, 67)], [(1, 62), (2.75, 59)],
]
# Eight sections, 22.5 seconds each. The first change is audible at 0:22.5,
# rather than making the listener wait through the same first 45 seconds.
sections = [
    ('Keyboard theme', ['C6', 'Am7', 'Fmaj7', 'G6'], opening[:8], 1.00, 'keys'),
    ('Nylon groove', ['C6', 'Am7', 'Dm7', 'G6'], [
        [(0, 67), (.75, 64), (1.5, 60), (2.75, 64)], [(0.5, 69), (1, 67), (2.5, 64)],
        [(0, 60), (.75, 64), (1.5, 67), (3.25, 69)], [(0.5, 67), (1.25, 64)],
        [(0, 65), (.75, 69), (2, 72), (2.75, 69)], [(0.5, 65), (1, 62), (2.5, 60)],
        [(0, 62), (.75, 67), (1.5, 64), (3.25, 62)], [(0.5, 59), (1.25, 55)],
    ], 1.02, 'nylon'),
    ('Reed melody', ['F6', 'Gm7', 'Bbmaj7', 'C6'], [
        [(0, 69), (2.5, 67)], [(0.5, 65)],
        [(0, 67), (2, 70)], [(0.75, 69), (2.75, 67)],
        [(0, 65), (2.5, 62)], [(0.5, 65)],
        [(0, 64), (2, 67)], [(0.75, 64), (2.75, 60)],
    ], .97, 'reed'),
    ('Moving strings', ['Dm7', 'Dm7', 'Bbmaj7', 'Gm7', 'F6', 'F6', 'C6', 'C6'], [
        [(0, 62), (.5, 65), (1.25, 69), (2.75, 65)], [(0.75, 62), (1.5, 60), (3, 57)],
        [(0, 62), (.5, 65), (1.25, 69), (2.75, 70)], [(0.75, 69), (1.5, 65)],
        [(0, 69), (.5, 67), (1.25, 65), (2.75, 62)], [(0.75, 60), (1.5, 62), (3, 65)],
        [(0, 67), (.5, 64), (1.25, 60), (2.75, 64)], [(0.75, 62), (1.5, 60)],
    ], .99, 'strings'),
    ('Keyboard interlude', ['Fmaj7', 'C/E', 'Am7', 'G6'], [
        [(0, 60), (2.5, 64)], [(1, 69), (2.25, 67)],
        [(0.5, 64), (2, 60)], [],
        [(0, 57), (1.25, 60), (3, 64)], [(1.5, 67)],
        [(0, 62), (2.5, 59)], [],
    ], .96, 'solo'),
    ('Instrument dialogue', ['Dm7', 'F6', 'G6', 'C6', 'Em7', 'G6', 'Am7', 'C6'], [
        [(0, 62), (1.25, 65), (2.5, 69)], [(0.5, 65), (2.5, 62)],
        [(0, 59), (1.25, 62), (2.5, 67)], [(0.5, 64), (2.5, 59)],
        [(0, 64), (1.25, 67), (2.5, 71)], [(0.5, 67), (2.5, 64)],
        [(0, 60), (1.25, 64), (2.5, 69)], [(0.5, 67), (2.5, 64)],
    ], .99, 'duet'),
    ('Theme with strings', ['C6', 'Am7', 'Fmaj7', 'G6'], [
        [(0.5, 64), (1.5, 67), (3, 69)], [(0.75, 72), (2, 69), (3, 67)],
        [(0.5, 60), (2, 64), (3, 67)], [(0.75, 69), (2, 67), (3.25, 64)],
        [(0.5, 57), (1.5, 60), (3, 64)], [(0.75, 69), (2, 67), (3, 64)],
        [(0.5, 62), (2, 64), (3, 67)], [(0.75, 71), (2.25, 67), (3.25, 62)],
    ], 1.00, 'return'),
    ('Gentle handover', ['Fmaj7', 'Dm7', 'C6', 'G6'], [
        [(0.5, 69), (1.75, 67), (3, 64)], [(1, 60), (2.75, 64)],
        [(0.75, 65), (2, 62), (3.25, 60)], [(1, 62), (2.5, 65)],
        [(0.5, 64), (1.75, 67), (3, 69)], [(1, 67), (2.75, 64)],
        [(0.75, 62), (2.25, 64), (3.25, 67)], [(1, 62), (2.5, 59), (3.5, 55)],
    ], .96, 'outro'),
]
assert len(sections) * 8 == total_bars
assert all(len(phrases) == 8 and len(progression) in (4, 8) for _, progression, phrases, _, _ in sections)

for bar in range(total_bars):
    section_index, local_bar = divmod(bar, 8)
    name, progression, phrases, level, arrangement = sections[section_index]
    phrase = phrases[local_bar]
    at = bar * 4 * beat
    root, chord = chords[progression[local_bar if len(progression) == 8 else local_bar // 2]]
    lead = 'keys'
    if arrangement in ('nylon', 'strings') or (arrangement == 'outro' and local_bar < 4):
        lead = 'nylon'
    elif arrangement == 'reed' or (arrangement == 'duet' and local_bar % 2 == 1):
        lead = 'reed'

    # Bass actually leaves the arrangement for parts of the interlude.
    bass_pattern = [(0, .18, 0), (2, .13, 0)]
    if arrangement in ('nylon', 'strings'):
        bass_pattern = [(0, .145, 0), (1.75, .075, 7), (3, .10, 0)]
    elif arrangement == 'reed':
        bass_pattern = [(0, .14, 0)]
    elif arrangement == 'solo':
        bass_pattern = [(0, .11, 0)] if local_bar in (2, 4, 6) else []
    elif arrangement == 'duet':
        bass_pattern = [(0, .14, 0), (2.75, .09, 7)]
    elif arrangement == 'return':
        bass_pattern = [(0, .16, 0), (2.5, .11, 7)]
    for step, velocity, interval in bass_pattern:
        mix_music(soft_key(root + interval, 1.3, bass=True), at + step * beat, velocity * level)

    # Each setting has a different texture, not the same chord strokes with
    # slightly different timing. Pads and rhythm are not present everywhere.
    if arrangement in ('nylon', 'strings', 'return'):
        steps = [0, 1.5, 2.5, 3.5] if arrangement == 'strings' else [.25, 1.75, 3]
        for stroke, step in enumerate(steps):
            note = chord[(stroke + local_bar) % 4]
            mix_music(nylon_pluck(note, 1.4), at + step * beat, .082 * level, -.25)
    elif arrangement == 'reed':
        for step in [.75, 2.75]:
            for index, note in enumerate(chord[:3]):
                mix_music(nylon_pluck(note, 2), at + step * beat + index * .025, .047 * level, -.2)
    elif arrangement == 'solo':
        for index, note in enumerate(chord[:3]):
            mix_music(soft_key(note, 2.5), at + (.1 if local_bar % 2 == 0 else 1.5) * beat + index * .035,
                      .07 * level, -.10)
    elif arrangement == 'duet':
        for step, note in [(0.25, chord[0]), (2.25, chord[2])]:
            mix_music(nylon_pluck(note, 1.7), at + step * beat, .07 * level, -.2)
    else:
        for step in [0, 2.5]:
            for index, note in enumerate(chord[:3]):
                mix_music(soft_key(note, 1.7), at + step * beat + index * .018,
                          (.055 if step == 0 else .042) * level, -.2)

    # Restrained brush strokes appear for the plucked sections and disappear
    # with the sustained reed and piano interlude. No hats, cymbals or fills.
    if arrangement in ('nylon', 'strings') and local_bar != 7:
        for step in [1, 3]:
            mix_music(brushed_tap(), at + step * beat, .065 * level, .10)
    elif arrangement == 'duet' and local_bar % 2 == 0:
        mix_music(brushed_tap(), at + 3 * beat, .050 * level, .10)

    for index, (step, note) in enumerate(phrase):
        # Tiny timing/velocity variation keeps the tune from feeling mechanical.
        offset = at + step * beat + music_rng.uniform(-.009, .009)
        seconds = 2.1
        if lead == 'reed':
            next_step = phrase[index + 1][0] if index + 1 < len(phrase) else 4
            seconds = min(2.15, max(.55, (next_step - step - .2) * beat))
        gain = (.19 if index == 0 else .16) * music_rng.uniform(.94, 1.04) * level
        mix_music(voice_clip(lead, note, seconds), offset, gain, .12 if lead != 'reed' else -.08)

    if arrangement == 'duet' and local_bar % 2 == 0:
        mix_music(warm_reed(chord[2], .8), at + 3.15 * beat, .065 * level, .15)
    if arrangement == 'return' and local_bar % 2 == 1:
        mix_music(nylon_pluck(chord[1], 1.4), at + 3.65 * beat, .085 * level, -.22)

    if arrangement in ('keys', 'strings', 'outro') and bar % 2 == 0:
        # Only these sections have a quiet harmonic cushion.
        seconds = (4 if len(progression) == 8 else 8) * beat + .8
        time = np.arange(round(seconds * music_rate)) / music_rate
        env = np.minimum(1, time / 1.2) * np.minimum(1, (seconds - time) / 1.6)
        for note in chord[1:3]:
            hz = 440 * 2 ** ((note - 69) / 12)
            pad = np.sin(2 * np.pi * hz * time) * env
            mix_music(pad, at, (.016 if arrangement == 'strings' else .022) * level, -.08)

# Quiet room reflections; all tails wrap so the tune loops continuously.
dry = music.copy()
for delay, gain in [(.127, .10), (.283, .055), (.463, .03)]:
    music += np.roll(dry[:, ::-1], round(delay * music_rate), axis=0) * gain
write('SandGarden', music, .52, music=True, sample_rate=music_rate)

folder_meta = OUT.with_suffix('.meta')
if not folder_meta.exists():
    folder_meta.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid5(uuid.NAMESPACE_URL, 'sand-game/audio').hex +
                          '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', encoding='utf-8')
