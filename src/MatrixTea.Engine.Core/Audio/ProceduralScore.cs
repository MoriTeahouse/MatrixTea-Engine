// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
using MatrixTea.Engine.Core.IO;

namespace MatrixTea.Engine.Core.Audio;

/// <summary>Deterministic original instrumental sketch. PCM16 mono, plucked melody, pads and soft percussion.</summary>
public static class ProceduralScore
{
    public static void WriteWave(string path, double seconds, double bpm, int rootMidi, int variation = 0)
    {
        if (!double.IsFinite(seconds) || seconds <= 0 || seconds > 600 || !double.IsFinite(bpm) || bpm < 30 || bpm > 300 || rootMidi is < 24 or > 84)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        const int sampleRate = 22050; int samples = checked((int)(seconds * sampleRate));
        int[] scale = { 0, 2, 4, 7, 9, 7, 4, 2, 0, 4, 9, 12, 11, 7, 4, 2 };
        int[] chords = { 0, -5, -3, -7 }; double beat = 60 / bpm;
        double[] melody = scale.Select(interval => Frequency(rootMidi + 12 + interval)).ToArray();
        double[] bassFrequencies = chords.Select(chord => Frequency(rootMidi - 12 + chord)).ToArray();
        double[] padRoots = chords.Select(chord => Frequency(rootMidi + chord)).ToArray();
        double[] padFifths = chords.Select(chord => Frequency(rootMidi + chord + 7)).ToArray();
        variation = ((variation % 16) + 16) % 16;
        AtomicFile.Write(path, stream =>
        {
            using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(sampleRate); writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
            for (int i = 0; i < samples; i++)
            {
                double t = i / (double)sampleRate, beatNumber = t / beat;
                int step = (int)(beatNumber * 2), chord = ((int)beatNumber / 8 + variation) % chords.Length;
                double local = t - step * beat / 2, note = melody[(step + variation * 3) % melody.Length];
                double pluck = (Math.Sin(t * note * Math.Tau) + 0.24 * Math.Sin(t * note * Math.Tau * 2)) * Math.Exp(-local * 11) * Math.Min(1, local * 300);
                double bass = Math.Sin(t * bassFrequencies[chord] * Math.Tau) * 0.15;
                double pad = (Math.Sin(t * padRoots[chord] * Math.Tau) + Math.Sin(t * padFifths[chord] * Math.Tau)) * 0.055;
                double drumTime = (beatNumber % 1) * beat;
                double kick = Math.Sin(Math.Tau * (55 * drumTime + 5 * (1 - Math.Exp(-drumTime * 30)))) * Math.Exp(-drumTime * 30) * 0.2;
                double fade = Math.Min(1, t / 0.12) * Math.Clamp((seconds - t) / 1.5, 0, 1);
                double sample = (pluck * 0.28 + bass + pad + kick) * fade;
                writer.Write((short)(Math.Clamp(sample, -0.98, 0.98) * short.MaxValue));
            }
        });
    }
    private static double Frequency(int midi) => 440 * Math.Pow(2, (midi - 69) / 12d);
}
