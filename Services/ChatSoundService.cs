using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Audio;

namespace NhxQualityPack
{
    // Plays the notification sound embedded in the DLL (Assets\ChatMessage.wav). Unity can't decode compressed audio
    // from memory, so the sound is a plain 16-bit PCM WAV whose samples are read here and handed to AudioClip.Create.
    internal static class ChatSoundService
    {
        private const string ResourceName = "NhxQualityPack.ChatMessage.wav";

        // WAV chunk tags, as the little-endian integers their four ASCII characters read as.
        private const uint RiffTag = 0x46464952; // "RIFF"
        private const uint WaveTag = 0x45564157; // "WAVE"
        private const uint FmtTag = 0x20746D66; // "fmt "
        private const uint DataTag = 0x61746164; // "data"

        // AudioClip.SetData(float[], int) can't be called directly: the game's Unity version also has a
        // ReadOnlySpan<float> overload, and ReadOnlySpan doesn't exist in the .NET Framework this project compiles
        // against, so the compiler can't resolve the call.
        private static readonly MethodInfo SetDataMethod = AccessTools.Method(typeof(AudioClip), nameof(AudioClip.SetData), new[] { typeof(float[]), typeof(int) });

        private static AudioClip _clip;
        private static AudioSource _source;
        private static AudioMixerGroup _guiGroup;
        private static bool _loadFailed;
        private static int _lastPlayedFrame = -1;

        // Called while a chat line is being written, so a failure here is logged rather than allowed to lose the line.
        internal static void Play()
        {
            // Messages shown together (e.g. the ones held while respawning) get a single sound.
            if (Time.frameCount == _lastPlayedFrame)
            {
                return;
            }

            _lastPlayedFrame = Time.frameCount;

            try
            {
                AudioClip clip = GetClip();

                if (clip == null)
                {
                    return;
                }

                AudioSource source = GetSource();
                source.outputAudioMixerGroup = GetGuiGroup();
                source.PlayOneShot(clip);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Failed to play the chat sound: {e.Message}");
            }
        }

        private static AudioClip GetClip()
        {
            if (_clip != null || _loadFailed)
            {
                return _clip;
            }

            try
            {
                _clip = LoadClip();
            }
            catch (Exception e)
            {
                _loadFailed = true;
                Plugin.Log.LogWarning($"Failed to load the chat sound: {e.Message}");
            }

            return _clip;
        }

        private static AudioSource GetSource()
        {
            if (_source != null)
            {
                return _source;
            }

            GameObject host = new GameObject("NhxQualityPack.ChatSound");
            UnityEngine.Object.DontDestroyOnLoad(host);

            _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.bypassReverbZones = true;

            // Highest priority, so a busy scene running out of voices never culls the alert.
            _source.priority = 0;

            return _source;
        }

        // The master mixer's GUI group, whose volume (GuiVol) the game's sound effects slider sets. AudioMan.m_guiMixer
        // looks like the way in, but the game leaves it unassigned on its _AudioManager prefab, so the group is looked
        // up by its path instead. Without it the sound still plays, just not following that slider.
        private static AudioMixerGroup GetGuiGroup()
        {
            if (_guiGroup == null && AudioMan.instance != null && AudioMan.instance.m_masterMixer != null)
            {
                AudioMixerGroup[] groups = AudioMan.instance.m_masterMixer.FindMatchingGroups("Master/GUI");
                _guiGroup = groups != null && groups.Length > 0 ? groups[0] : null;
            }

            return _guiGroup;
        }

        private static AudioClip LoadClip()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                {
                    throw new InvalidDataException($"embedded resource {ResourceName} not found");
                }

                using (BinaryReader reader = new BinaryReader(stream))
                {
                    if (reader.ReadUInt32() != RiffTag)
                    {
                        throw new InvalidDataException("not a RIFF file");
                    }

                    reader.ReadInt32();

                    if (reader.ReadUInt32() != WaveTag)
                    {
                        throw new InvalidDataException("not a WAVE file");
                    }

                    int channels = 0;
                    int sampleRate = 0;
                    byte[] data = null;

                    while (data == null && stream.Position + 8 <= stream.Length)
                    {
                        uint tag = reader.ReadUInt32();
                        int size = reader.ReadInt32();
                        long next = stream.Position + size + (size & 1);

                        if (tag == FmtTag)
                        {
                            short format = reader.ReadInt16();
                            channels = reader.ReadInt16();
                            sampleRate = reader.ReadInt32();
                            reader.ReadInt32();
                            reader.ReadInt16();
                            short bitsPerSample = reader.ReadInt16();

                            if (format != 1 || bitsPerSample != 16)
                            {
                                throw new InvalidDataException("only 16-bit PCM WAV files are supported");
                            }
                        }
                        else if (tag == DataTag)
                        {
                            data = reader.ReadBytes(size);
                        }

                        stream.Position = next;
                    }

                    if (channels <= 0 || sampleRate <= 0 || data == null)
                    {
                        throw new InvalidDataException("missing fmt or data chunk");
                    }

                    float[] samples = new float[data.Length / 2];

                    for (int i = 0; i < samples.Length; i++)
                    {
                        samples[i] = (short)(data[i * 2] | (data[i * 2 + 1] << 8)) / 32768f;
                    }

                    AudioClip clip = AudioClip.Create("NhxQualityPack.ChatMessage", samples.Length / channels, channels, sampleRate, false);
                    SetDataMethod.Invoke(clip, new object[] { samples, 0 });
                    clip.hideFlags = HideFlags.DontUnloadUnusedAsset;

                    return clip;
                }
            }
        }
    }
}
