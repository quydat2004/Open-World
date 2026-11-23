#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HueCity.EditorTools
{
    /// <summary>
    /// Trình tạo âm thanh tự động (Procedural Audio Generator).
    /// Tổng hợp 5 file âm thanh PCM WAV chất lượng cao chuẩn game:
    /// 1. SFX_Engine_Idle_Loop.wav: Tiếng động cơ xe nổ êm dịu.
    /// 2. SFX_Car_Horn.wav: Tiếng còi xe đô thị hòa âm 2 nốt F4/A4.
    /// 3. SFX_Tire_Skid.wav: Tiếng rít lốp phanh gấp trên mặt đường.
    /// 4. SFX_Rain_Ambient_Loop.wav: Tiếng mưa rơi rào rào Cố Đô Huế.
    /// 5. SFX_Footstep_Step.wav: Tiếng bước chân người đi bộ trên vỉa hè.
    /// </summary>
    public static class ProceduralAudioGenerator
    {
        private const string AudioDir = "Assets/_Project/Audio";

        [InitializeOnLoadMethod]
        private static void AutoGenerateAudio()
        {
            EditorApplication.delayCall += () =>
            {
                EnsureAudioAssets();
            };
        }

        [MenuItem("HueCity/Generate Procedural Audio Clips", false, 65)]
        public static void EnsureAudioAssets()
        {
            if (!Directory.Exists(AudioDir))
            {
                Directory.CreateDirectory(AudioDir);
            }

            bool generatedAny = false;

            generatedAny |= GenerateEngineIdleWav(Path.Combine(AudioDir, "SFX_Engine_Idle_Loop.wav"));
            generatedAny |= GenerateCarHornWav(Path.Combine(AudioDir, "SFX_Car_Horn.wav"));
            generatedAny |= GenerateTireSkidWav(Path.Combine(AudioDir, "SFX_Tire_Skid.wav"));
            generatedAny |= GenerateRainAmbientWav(Path.Combine(AudioDir, "SFX_Rain_Ambient_Loop.wav"));
            generatedAny |= GenerateFootstepWav(Path.Combine(AudioDir, "SFX_Footstep_Step.wav"));

            if (generatedAny)
            {
                AssetDatabase.Refresh();
                Debug.Log("<color=green>[HueCity Audio]</color> Đã tổng hợp thành công trọn bộ 5 file âm thanh WAV chất lượng cao!");
            }
        }

        private static bool GenerateEngineIdleWav(string path)
        {
            if (File.Exists(path)) return false;

            int sampleRate = 22050;
            float duration = 1.0f; // 1 giây lặp
            int totalSamples = (int)(sampleRate * duration);
            short[] samples = new short[totalSamples];

            float baseFreq = 48f; // Tần số nổ máy chạy không tải (48 Hz)

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;

                // Hòa âm 4 kỳ của piston: bậc 1, bậc 2 và bậc 3
                float wave = Mathf.Sin(2f * Mathf.PI * baseFreq * t) * 0.45f
                           + Mathf.Sin(2f * Mathf.PI * baseFreq * 2f * t) * 0.30f
                           + Mathf.Sin(2f * Mathf.PI * baseFreq * 4f * t) * 0.15f;

                // Thêm chút rung động cơ học (mechanical rumble noise)
                float noise = ((float)new System.Random(i).NextDouble() * 2f - 1f) * 0.10f;
                float finalSample = Mathf.Clamp(wave + noise, -1f, 1f);

                samples[i] = (short)(finalSample * 32767f);
            }

            WriteWavFile(path, sampleRate, samples);
            return true;
        }

        private static bool GenerateCarHornWav(string path)
        {
            if (File.Exists(path)) return false;

            int sampleRate = 22050;
            float duration = 0.65f; // 650ms tiếng còi
            int totalSamples = (int)(sampleRate * duration);
            short[] samples = new short[totalSamples];

            // Hòa âm nốt Fa 4 (349 Hz) và La 4 (440 Hz) đặc trưng còi xe hơi
            float freq1 = 349.23f;
            float freq2 = 440.0f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;

                // Envelope ADSR: Attack 15ms, Decay 50ms, Release 80ms
                float env = 1.0f;
                if (t < 0.02f) env = t / 0.02f;
                else if (t > duration - 0.08f) env = (duration - t) / 0.08f;

                float wave = Mathf.Sin(2f * Mathf.PI * freq1 * t) * 0.5f
                           + Mathf.Sin(2f * Mathf.PI * freq2 * t) * 0.4f
                           + Mathf.Sin(2f * Mathf.PI * (freq1 * 2f) * t) * 0.1f;

                float finalSample = Mathf.Clamp(wave * env, -1f, 1f);
                samples[i] = (short)(finalSample * 32767f);
            }

            WriteWavFile(path, sampleRate, samples);
            return true;
        }

        private static bool GenerateTireSkidWav(string path)
        {
            if (File.Exists(path)) return false;

            int sampleRate = 22050;
            float duration = 0.85f;
            int totalSamples = (int)(sampleRate * duration);
            short[] samples = new short[totalSamples];

            System.Random rand = new System.Random(12345);
            float lastVal = 0f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin((t / duration) * Mathf.PI); // Hình cung tròn

                // Tiếng rít ma sát cao tần (Screech around 1400Hz - 2200Hz)
                float chirpFreq = Mathf.Lerp(2100f, 1300f, t / duration);
                float screech = Mathf.Sin(2f * Mathf.PI * chirpFreq * t) * 0.4f;

                // Lọc thông dải noise ma sát cao su với mặt đường
                float whiteNoise = (float)rand.NextDouble() * 2f - 1f;
                lastVal = Mathf.Lerp(lastVal, whiteNoise, 0.45f);

                float finalSample = Mathf.Clamp((screech + lastVal * 0.5f) * env * 0.75f, -1f, 1f);
                samples[i] = (short)(finalSample * 32767f);
            }

            WriteWavFile(path, sampleRate, samples);
            return true;
        }

        private static bool GenerateRainAmbientWav(string path)
        {
            if (File.Exists(path)) return false;

            int sampleRate = 22050;
            float duration = 2.0f; // 2 giây loop
            int totalSamples = (int)(sampleRate * duration);
            short[] samples = new short[totalSamples];

            System.Random rand = new System.Random(54321);
            float lowPass1 = 0f;
            float lowPass2 = 0f;

            for (int i = 0; i < totalSamples; i++)
            {
                // Pink / Brown noise lọc thông dải mô phỏng hạt mưa rơi rào rào
                float white = (float)rand.NextDouble() * 2f - 1f;
                lowPass1 = lowPass1 * 0.82f + white * 0.18f;
                lowPass2 = lowPass2 * 0.94f + white * 0.06f;

                // Thỉnh thoảng có giọt mưa lộp độp rơi vào mái tôn/mặt đường
                float drop = (rand.Next(0, 180) == 0) ? (float)rand.NextDouble() * 0.4f : 0f;

                float rainSound = (lowPass1 * 0.65f + lowPass2 * 0.35f + drop) * 0.7f;
                samples[i] = (short)(Mathf.Clamp(rainSound, -1f, 1f) * 32767f);
            }

            WriteWavFile(path, sampleRate, samples);
            return true;
        }

        private static bool GenerateFootstepWav(string path)
        {
            if (File.Exists(path)) return false;

            int sampleRate = 22050;
            float duration = 0.22f; // 220ms một bước gót chạm đất
            int totalSamples = (int)(sampleRate * duration);
            short[] samples = new short[totalSamples];

            System.Random rand = new System.Random(9999);
            float lowPass = 0f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;

                // Envelope va chạm tiếp đất (thump + click)
                float env = Mathf.Exp(-t * 28f);

                // Âm va chạm trầm (80Hz) + tiếng đế giày sột soạt trên bê tông
                float thump = Mathf.Sin(2f * Mathf.PI * 85f * t) * 0.6f;
                float noise = (float)rand.NextDouble() * 2f - 1f;
                lowPass = lowPass * 0.7f + noise * 0.3f;

                float finalSample = Mathf.Clamp((thump + lowPass * 0.4f) * env * 0.85f, -1f, 1f);
                samples[i] = (short)(finalSample * 32767f);
            }

            WriteWavFile(path, sampleRate, samples);
            return true;
        }

        private static void WriteWavFile(string filePath, int sampleRate, short[] samples)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                int byteRate = sampleRate * 1 * 2; // Mono 16-bit
                int dataSize = samples.Length * 2;

                // RIFF chunk
                bw.Write(new char[] { 'R', 'I', 'F', 'F' });
                bw.Write(36 + dataSize);
                bw.Write(new char[] { 'W', 'A', 'V', 'E' });

                // fmt chunk
                bw.Write(new char[] { 'f', 'm', 't', ' ' });
                bw.Write(16); // Subchunk1Size (16 for PCM)
                bw.Write((short)1); // AudioFormat (1 for PCM)
                bw.Write((short)1); // NumChannels (1 = Mono)
                bw.Write(sampleRate);
                bw.Write(byteRate);
                bw.Write((short)2); // BlockAlign
                bw.Write((short)16); // BitsPerSample

                // data chunk
                bw.Write(new char[] { 'd', 'a', 't', 'a' });
                bw.Write(dataSize);

                for (int i = 0; i < samples.Length; i++)
                {
                    bw.Write(samples[i]);
                }
            }
        }
    }
}
#endif
