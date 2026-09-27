using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DiGraphLab.Harmony.Viewer.Playback
{
    // Simple sine-wave polyphonic synthesizer using NAudio WaveOut
    public static class SimpleSynth
    {
        private static WaveOutEvent? _out;
        private static MixingSampleProvider? _mixer;
        // global synth/effect settings
        public enum Waveform { Sine, Square, Saw, Triangle }
        public static Waveform CurrentWaveform = Waveform.Sine;
        public static int DelayMs = 0;
        public static float DelayFeedback = 0.3f;
        public static float ReverbMix = 0.0f; // unused advanced

        public static void Initialize(int sampleRate = 44100)
        {
            if (_out != null) return;
            _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2)) { ReadFully = true };
            // wrap mixer with effects provider
            var effected = new EffectsSampleProvider(_mixer);
            _out = new WaveOutEvent();
            // WaveOutEvent.Init requires an IWaveProvider; convert the ISampleProvider
            _out.Init(new SampleToWaveProvider(effected));
            _out.Play();
        }

        public static void PlaySemitones(IEnumerable<int> semitones, int durationMs = 800, int sampleRate = 44100)
        {
            Initialize(sampleRate);
            var list = semitones.ToArray();
            if (list.Length == 0) return;
            var providers = new List<ISampleProvider>();
            foreach (var st in list)
            {
                var freq = 440.0 * Math.Pow(2.0, (st - 69) / 12.0);
                var wave = new SignalGenerator(sampleRate, 2, CurrentWaveform).SetFrequency(freq).SetAmplitude(0.15);
                var envelope = new AmplitudeEnvelope(wave, durationMs / 1000.0);
                providers.Add(envelope);
            }
            var mixed = new ConcatenatingSampleProvider(providers);
            if (_mixer != null)
            {
                _mixer.AddMixerInput(mixed);
            }
        }
    }

    // small utility generators
    internal class SignalGenerator : ISampleProvider
    {
        private readonly int _sampleRate;
        private readonly int _channels;
        private double _freq = 440.0;
        private double _amp = 0.2;
        private double _phase;
        private readonly SimpleSynth.Waveform _waveform;
        public WaveFormat WaveFormat { get; }
        public SignalGenerator(int sampleRate, int channels)
            : this(sampleRate, channels, SimpleSynth.CurrentWaveform)
        {
        }

        public SignalGenerator(int sampleRate, int channels, SimpleSynth.Waveform waveform)
        {
            _sampleRate = sampleRate; _channels = channels;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
            _waveform = waveform;
        }
        public SignalGenerator SetFrequency(double f) { _freq = f; return this; }
        public SignalGenerator SetAmplitude(double a) { _amp = a; return this; }
        public int Read(float[] buffer, int offset, int count)
        {
            for (int n = 0; n < count / _channels; n++)
            {
                double t = _phase / _sampleRate;
                double value = 0.0;
                switch (_waveform)
                {
                    case SimpleSynth.Waveform.Sine:
                        value = Math.Sin(2 * Math.PI * _freq * _phase / _sampleRate);
                        break;
                    case SimpleSynth.Waveform.Saw:
                        value = 2.0 * (_phase * _freq / _sampleRate - Math.Floor(0.5 + _phase * _freq / _sampleRate));
                        break;
                    case SimpleSynth.Waveform.Square:
                        value = Math.Sign(Math.Sin(2 * Math.PI * _freq * _phase / _sampleRate));
                        break;
                    case SimpleSynth.Waveform.Triangle:
                        value = 2.0 * Math.Abs(2.0 * (_phase * _freq / _sampleRate - Math.Floor(_phase * _freq / _sampleRate + 0.5))) - 1.0;
                        break;
                }
                var sample = (float)(_amp * value);
                _phase++;
                for (int c = 0; c < _channels; c++) buffer[offset + n * _channels + c] = sample;
            }
            return count;
        }
    }

    // Effects wrapper applying a simple delay+feedback and wet mix to an inner ISampleProvider.
    internal class EffectsSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly int _channels;
        private readonly int _sampleRate;
        private float[] _buffer = Array.Empty<float>();
        private int _writePos = 0;
        public WaveFormat WaveFormat => _source.WaveFormat;

        public EffectsSampleProvider(ISampleProvider source)
        {
            _source = source;
            _channels = source.WaveFormat.Channels;
            _sampleRate = source.WaveFormat.SampleRate;
            EnsureBuffer(SimpleSynth.DelayMs);
        }

        private void EnsureBuffer(int delayMs)
        {
            var needed = Math.Max(1, (int)(_sampleRate * (delayMs / 1000.0))) * _channels;
            if (_buffer.Length < needed) _buffer = new float[needed];
            if (_writePos >= _buffer.Length) _writePos = 0;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            // read source
            int read = _source.Read(buffer, offset, count);
            try
            {
                EnsureBuffer(SimpleSynth.DelayMs);
                if (SimpleSynth.DelayMs <= 0) return read;
                int delaySamples = (_sampleRate * SimpleSynth.DelayMs / 1000) * _channels;
                if (delaySamples <= 0) return read;
                for (int n = 0; n < read; n++)
                {
                    int bufIdx = (_writePos + n) % _buffer.Length;
                    float delayed = _buffer[bufIdx];
                    float dry = buffer[offset + n];
                    float wet = delayed * SimpleSynth.ReverbMix; // small reverb mix
                    // mix delayed into output
                    buffer[offset + n] = dry + delayed * 0.5f;
                    // write back into ring buffer with feedback
                    _buffer[bufIdx] = dry + delayed * SimpleSynth.DelayFeedback;
                }
                _writePos = (_writePos + read) % _buffer.Length;
            }
            catch { }
            return read;
        }
    }

    internal class AmplitudeEnvelope : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly int _sampleRate;
        private readonly int _totalSamples;
        private int _pos;
        public WaveFormat WaveFormat => _source.WaveFormat;
        public AmplitudeEnvelope(ISampleProvider source, double seconds)
        {
            _source = source; _sampleRate = source.WaveFormat.SampleRate; _totalSamples = (int)(seconds * _sampleRate);
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);
            for (int i = 0; i < read / WaveFormat.Channels; i++)
            {
                double t = (double)_pos / _totalSamples;
                double env = 1.0;
                if (t < 0.1) env = t / 0.1; else if (t > 0.9) env = (1.0 - t) / 0.1;
                for (int c = 0; c < WaveFormat.Channels; c++) buffer[offset + i * WaveFormat.Channels + c] *= (float)env;
                _pos++;
            }
            return read;
        }
    }

    internal class ConcatenatingSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider[] _sources;
        private int _index = 0;
        public WaveFormat WaveFormat => _sources[0].WaveFormat;
        public ConcatenatingSampleProvider(IEnumerable<ISampleProvider> sources)
        {
            _sources = sources.ToArray();
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count && _index < _sources.Length)
            {
                int r = _sources[_index].Read(buffer, offset + total, count - total);
                total += r;
                if (r == 0) _index++;
            }
            return total;
        }
    }
}
