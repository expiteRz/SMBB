using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NAudio.MediaFoundation;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SMBB.Properties;

namespace SMBB
{
    class Utils
    {
        public static byte[] byteArrayCat(params byte[][] src)
        {
            uint length = 0;
            uint offset = 0;
            for (uint i = 0; i < src.Length; i++) length += (uint)src[i].Length;
            byte[] dest = new byte[length];
            for (uint i = 0; i < src.Length; i++)
            {
                memcpy(dest, offset, src[i], 0, (uint)src[i].Length);
                offset += (uint)src[i].Length;
            }
            return dest;
        }
        public static void memcpy(byte[] dest, uint destOffset, byte[] src, uint srcOffset, uint length)
        {
            Array.Copy(src, srcOffset, dest, destOffset, length);
        }
        public unsafe static float bytesToFloat(byte[] data, uint offset)
        {
            uint tmpInt = bytesToUint(data, offset);
            float dest = *((float*)&tmpInt);
            return dest;
        }
        public static ulong bytesToUlong(byte[] data, uint offset)
        {
            ulong dest = 0;
            for (int i = 0; i < 8; i++)
            {
                dest <<= 8;
                dest += data[offset + (7 - i)];
            }
            return dest;
        }
        public static uint bytesToUint(byte[] data, uint offset)
        {
            uint dest = 0;
            for (int i = 0; i < 4; i++)
            {
                dest <<= 8;
                dest += data[offset + (3 - i)];
            }
            return dest;
        }
        public static uint bytesToUint24(byte[] data, uint offset)
        {
            uint dest = 0;
            for (int i = 0; i < 3; i++)
            {
                dest <<= 8;
                dest += data[offset + (2 - i)];
            }
            return dest;
        }
        public static ushort bytesToUshort(byte[] data, uint offset)
        {
            ushort dest = 0;
            for (int i = 0; i < 2; i++)
            {
                dest <<= 8;
                dest += data[offset + (1 - i)];
            }
            return dest;
        }
        public static string bytesToString(byte[] data, uint offset, uint length)
        {
            string dest = "";
            char tmpChar;
            for (uint i = 0; i < length; i++)
            {
                if (data[offset + i] == 0) break;
                tmpChar = (char)data[offset + i];
                dest += new string(tmpChar, 1);
            }
            return dest;
        }
        public static void ushortToBytes(byte[] data, uint offset, ushort val)
        {
            for (int i = 0; i < 2; i++)
            {
                data[offset + i] = (byte)((val >> (8 * i)) & 0xFF);
            }
        }
        public static void uintToBytes(byte[] data, uint offset, uint val)
        {
            for (int i = 0; i < 4; i++)
            {
                data[offset + i] = (byte)((val >> (8 * i)) & 0xFF);
            }
        }
        public static void stringToBytes(byte[] data, uint offset, uint length, string val)
        {
            for (uint i = 0; i < length; i++)
            {
                if (i >= val.Length)
                {
                    data[offset + i] = 0;
                }
                else
                {
                    data[offset + i] = (byte)val[(int)i];
                }
            }
        }
    }
    class Sound
    {
        public const string RIFF_TAG = "RIFF";
        public const string WAVE_TAG = "WAVE";
        public const string DATA_TAG = "data";
        public const string FMT_TAG = "fmt ";
        public const ushort PCM_8 = 0;
        public const ushort PCM_16 = 1;
        public const ushort PCM_24 = 65534;
        public const ushort PCM_32 = 65533;
        public const ushort PCM_64 = 65532;
        //public const ushort MS_ADPCM = 2;
        public const ushort PCM_FLOAT = 3;
        public const byte NO_ERROR = 0;
        public const byte ERROR_OPEN_FILE = 1;
        public const byte ERROR_INVALID_FILE = 2;
        public const byte ERROR_INVALID_ARGS = 3;
        public byte error = ERROR_INVALID_ARGS;
        public uint sampleRate;
        public uint sampleLength;
        public ushort channelCount;
        public ushort format;
        public byte[] data = null;
        public Sound(){}
        public Sound(uint _sampleRate, uint _sampleLength, ushort _channelCount, ushort _format)
        {
            if (_format != PCM_8 && _format != PCM_16 && _format != PCM_24 && _format != PCM_32 && _format != PCM_64 && _format != PCM_FLOAT)
            {
                error = ERROR_INVALID_ARGS;
                return;
            }
            sampleRate = _sampleRate;
            sampleLength = _sampleLength;
            channelCount = _channelCount;
            format = _format;
            uint bytePerSample = getBps() / 8;
            data = new byte[bytePerSample * channelCount * sampleLength];
            error = NO_ERROR;
        }
        public Sound(string path)
        {
            byte[] src;
            
            try
            {
                if (path.Contains(".mp4") || path.Contains(".m4a") || path.Contains(".aac"))
                {
                    src = ReadAacFromFile(path);
                }
                else src = File.ReadAllBytes(path);
            }
            catch
            {
                error = ERROR_OPEN_FILE;
                return;
            }
            readSoundFromBytes(src);
        }
        public Sound(byte[] src)
        {
            readSoundFromBytes(src);
        }
        uint getBps()
        {
            switch (format)
            {
                case PCM_8:
                    return 8;
                case PCM_16:
                    return 16;
                case PCM_24:
                    return 24;
                case PCM_32:
                    return 32;
                case PCM_64:
                    return 64;
                case PCM_FLOAT:
                    return 32;
            }
            return 0;
        }
        void readSoundFromBytes(byte[] src)
        {
            readWavFromBytes(src);
            if (error == NO_ERROR) return;
            try
            {
                readMp3FromBytes(src);
            }
            catch
            {
                return;
            }
            error = NO_ERROR;
        }
        void readWavFromBytes(byte[] src)
        {
            error = ERROR_INVALID_FILE;
            if (src.Length < 0x14) return;
            if (Utils.bytesToString(src, 0, 4) != RIFF_TAG) return;
            if (Utils.bytesToString(src, 8, 4) != WAVE_TAG) return;
            uint curElementOffset = 0xC;
            uint nextElementOffset;
            while (true)
            {
                string curElementName;
                uint curElementSize;
                if (curElementOffset + 8 > src.Length) break;
                curElementSize = Utils.bytesToUint(src, curElementOffset + 4);
                nextElementOffset = curElementOffset + curElementSize + 8;
                if (nextElementOffset > src.Length) break;
                curElementName = Utils.bytesToString(src, curElementOffset, 4);
                if (curElementName == FMT_TAG)
                {
                    if (curElementSize < 0x10) return;
                    format = Utils.bytesToUshort(src, curElementOffset + 8);
                    ushort bps = Utils.bytesToUshort(src, curElementOffset + 22);
                    if (format == PCM_16)
                    {
                        if (bps == 8 || bps == 16 || bps == 24 || bps == 32 || bps == 64)
                        {
                            error = NO_ERROR;
                            switch (bps)
                            {
                                case 8:
                                    format = PCM_8;
                                    break;
                                case 24:
                                    format = PCM_24;
                                    break;
                                case 32:
                                    format = PCM_32;
                                    break;
                                case 64:
                                    format = PCM_64;
                                    break;
                            }
                        }
                    }else if (format == PCM_FLOAT)
                    {
                        if(bps == 32)error = NO_ERROR;
                    }
                    channelCount = Utils.bytesToUshort(src, curElementOffset + 10);
                    sampleRate = Utils.bytesToUint(src, curElementOffset + 12);
                }
                else if (curElementName == DATA_TAG)
                {
                    data = new byte[curElementSize];
                    Utils.memcpy(data, 0, src, curElementOffset + 8, curElementSize);
                    if (format == PCM_8 || format == PCM_16 || format == PCM_24 || format == PCM_32 || format == PCM_64 || format == PCM_FLOAT)
                    {
                        uint bps = getBps() / 8;
                        bps *= channelCount;
                        sampleLength = curElementSize / bps;
                    }
                }
                curElementOffset = nextElementOffset;
            }
            if (data == null) error = ERROR_INVALID_FILE;
        }
        void readMp3FromBytes(byte[] src)
        {
            using (Stream stream = new MemoryStream())
            {
                stream.Write(src, 0, src.Length);

                stream.Position = 0;

                using (WaveStream pcm = new Mp3FileReader(stream))
                {
                    data = new byte[Convert.ToInt32(pcm.Length)];
                    pcm.Read(data, 0, Convert.ToInt32(pcm.Length));
                    channelCount = (ushort)pcm.WaveFormat.Channels;
                    sampleRate = (uint)pcm.WaveFormat.SampleRate;
                }
            }
            sampleLength = (uint)(data.Length / (channelCount * 2));
            format = PCM_16;
        }

        byte[] ReadAacFromFile(string path)
        {
            try
            {
                using (var reader = new MediaFoundationReader(path))
                {
                    using (var resampler = new ResamplerDmoStream(reader, new WaveFormat(reader.WaveFormat.SampleRate,
                        reader.WaveFormat.BitsPerSample, reader.WaveFormat.Channels)))
                    {
                        using (var ms = new MemoryStream())
                        using (var waveWriter = new WaveFileWriter(ms, resampler.WaveFormat))
                        {
                            resampler.CopyTo(waveWriter);
                            
                            var currentPos = ms.Position;
                            try
                            {
                                ms.Position = 0x4;
                                uint i = (uint) waveWriter.Length + 0x26;
                                var bytes = BitConverter.GetBytes(i);
                                var writer = new BinaryWriter(ms);
                                writer.Write(bytes);

                                ms.Position = 0x2b;
                                var j = bytes.Skip(1).ToArray();
                                writer = new BinaryWriter(ms);
                                writer.Write(j);
                            }
                            finally
                            {
                                ms.Position = currentPos;
                            }
                            
                            return ms.ToArray();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                throw;
            }
        }
        
        public Sound[] spilitChannel()
        {
            Sound[] dest = new Sound[channelCount];
            for (uint i = 0; i < channelCount; i++)
            {
                dest[i] = new Sound(sampleRate, sampleLength, 1, format);
                if (format == PCM_8 || format == PCM_16 || format == PCM_24 || format == PCM_32 || format == PCM_64 || format == PCM_FLOAT)
                {
                    uint bytePerSample = getBps() / 8;
                    for (uint j = 0; j < sampleLength; j++)
                    {
                        Utils.memcpy(dest[i].data, j * bytePerSample, data, (j * channelCount + i) * bytePerSample, bytePerSample);
                    }
                }
            }
            return dest;
        }
        public void toPCM16()
        {
            byte[] destData = new byte[channelCount * sampleLength * 2];
            if (format == PCM_8)
            {
                for (uint i = 0; i < data.Length; i++)
                {
                    short pcm = data[i];
                    pcm -= 128;
                    pcm *= 256;
                    Utils.ushortToBytes(destData, 2 * i, (ushort)pcm);
                }
            }
            else if (format == PCM_24 || format == PCM_32)
            {
                uint length;
                if (format == PCM_24)
                {
                    length = (uint)(data.Length / 3);
                }
                else
                {
                    length = (uint)(data.Length / 4);
                }
                for (uint i = 0; i < length; i++)
                {
                    uint pcm;
                    if (format == PCM_24)
                    {
                        pcm = Utils.bytesToUint24(data, 3 * i);
                        pcm >>= 8;
                    }
                    else
                    {
                        pcm = Utils.bytesToUint(data, 4 * i);
                        pcm >>= 16;
                    }
                    Utils.ushortToBytes(destData, 2 * i, (ushort)pcm);
                }
            }
            else if(format == PCM_64)
            {
                ulong pcm;
                for (uint i = 0; i < (data.Length / 8); i++)
                {
                    pcm = Utils.bytesToUlong(data, 8 * i);
                    pcm >>= 48;
                    Utils.ushortToBytes(destData, 2 * i, (ushort)pcm);
                }
            }
            else
            {
                float pcmFloat;
                short pcm;
                for (uint i = 0; i < (data.Length / 4); i++)
                {
                    pcmFloat = Utils.bytesToFloat(data, 4 * i);
                    pcm = (short)((32767 * pcmFloat) + 0.5);
                    Utils.ushortToBytes(destData, 2 * i, (ushort)pcm);
                }
            }
            if(format != PCM_16)data = destData;
            format = PCM_16;
        }
        int sampleToTime(int sample)
        {
            if (sample < 0 || sampleLength <= sample) return -1;
            float time = sample / (float)sampleRate;
            time *= 1000;
            time += 0.5f;
            return (int)time;
        }
        public byte[] saveAsWaveBytes()
        {
            byte[] waveHeader = new byte[12];
            byte[] fmtElement = null;
            byte[] dataElement = new byte[data.Length + 8];
            Utils.stringToBytes(waveHeader, 0, 4, RIFF_TAG);
            Utils.stringToBytes(waveHeader, 8, 4, WAVE_TAG);
            Utils.stringToBytes(dataElement, 0, 4, DATA_TAG);
            Utils.uintToBytes(dataElement, 4, (uint)data.Length);
            Utils.memcpy(dataElement, 8, data, 0, (uint)data.Length);
            if (format == PCM_8 || format == PCM_16 || format == PCM_24 || format == PCM_32 || format == PCM_64 || format == PCM_FLOAT)
            {
                ushort blockSize = (ushort)(getBps() / 8);
                blockSize *= channelCount;
                fmtElement = new byte[24];
                Utils.stringToBytes(fmtElement, 0, 4, FMT_TAG);
                Utils.uintToBytes(fmtElement, 4, 16);
                Utils.ushortToBytes(fmtElement, 8, PCM_16);
                if(format == PCM_FLOAT) Utils.ushortToBytes(fmtElement, 8, PCM_FLOAT);
                Utils.ushortToBytes(fmtElement, 0xA, channelCount);
                Utils.uintToBytes(fmtElement, 0xC, sampleRate);
                Utils.uintToBytes(fmtElement, 0x10, blockSize * sampleRate);
                Utils.ushortToBytes(fmtElement, 0x14, blockSize);
                Utils.ushortToBytes(fmtElement, 0x16, (ushort)getBps());
            }
            return Utils.byteArrayCat(waveHeader, fmtElement, dataElement);
        }
        public bool saveAsWaveFile(string path)
        {
            try
            {
                File.WriteAllBytes(path, saveAsWaveBytes());
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        const int NO_PROGRESS = 0;
        const int SPILIT_WAV = -1;
        const int BUILD_BRSTM = -2;
        int progress = NO_PROGRESS;
        Regex notIntReg = new Regex(@"[^0-9]");
        string[] needToolFiles = {"DSPADPCM.exe", "dsptool.dll", "hio2.dll", "soundfile.dll", "wdrev.exe" };
        string toolsPath;
        string tmpPath;
        Sound srcWav = new Sound();
        byte[] lpFileData = {0x4C, 0x4F, 0x4F, 0x50, 0x3D, 0xDD, 0x96, 0x17, 0, 0, 0, 0, 0, 0, 0, 0};
        bool isLooped = false;
        bool finalLap = false;
        uint loopStart = 0;
        uint loopEnd = 0;
        uint realLoopStart;
        uint realLoopEnd;
        uint srcChannelCount;
        uint destChannelCount = 2;
        string brstmOutPath = "";
        public MainWindow()
        {
            InitializeComponent();
            toolsPath = AppDomain.CurrentDomain.BaseDirectory + "tools\\";
            tmpPath = AppDomain.CurrentDomain.BaseDirectory + "tmp\\";
            bool needToolFilesOK = true;
            string needToolFilesErrMsg = $"{Properties.Resources.NonToolsAlert}\n";
            for (int i = 0;i < needToolFiles.Length;i++)
            {
                if (!File.Exists(toolsPath + needToolFiles[i]))
                {
                    needToolFilesErrMsg += "\n";
                    needToolFilesErrMsg += needToolFiles[i];
                    needToolFilesOK = false;
                }
            }
            if (!needToolFilesOK)
            {
                MessageBox.Show(needToolFilesErrMsg, Properties.Resources.ErrorCapacity, MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }
        private void setUI()
        {
            if (buildBrstm == null) return;
            buildBrstm.IsEnabled = false;
            brstmPathShow.Text = brstmOutPath;
            sampleWarnText.Text = "";
            if (isLooped)
            {
                if (loopStart > loopEnd) sampleWarnText.Text = Properties.Resources.LoopStartAfterEndWarning;
                if (loopStart == loopEnd) sampleWarnText.Text = Properties.Resources.LoopValuesSameWarning;
                if (srcWav.error == Sound.NO_ERROR && loopEnd > srcWav.sampleLength) // sampleWarnText.Text = $"{Properties.Resources.LoopEndOverSourceWarning} ({srcWav.sampleLength} {Properties.Resources.SampleUnit})";
                    loopEnd = srcWav.sampleLength;
                loopStartText.IsEnabled = true;
                loopEndText.IsEnabled = true;
                lpLoadButton.IsEnabled = true;
                lpSaveButton.IsEnabled = true;
            }
            else
            {
                loopStartText.IsEnabled = false;
                loopEndText.IsEnabled = false;
                lpLoadButton.IsEnabled = false;
                lpSaveButton.IsEnabled = false;
            }
            loopStartText.Text = loopStart.ToString();
            loopEndText.Text = loopEnd.ToString();
            if(progress == NO_PROGRESS)
            {
                progressText.Text = "";
                wavButton.IsEnabled = true;
                brstmButton.IsEnabled = true;
                channelCountSelect.IsEnabled = true;
                finalLapCheckBox.IsEnabled = true;
                loopCheckBox.IsEnabled = true;
                if (brstmOutPath != "" && srcWav.error == Sound.NO_ERROR) buildBrstm.IsEnabled = true;
            }
            else
            {
                switch (progress)
                {
                    case SPILIT_WAV:
                        progressText.Text = Properties.Resources.ProgressWaveSplit;
                        break;
                    case BUILD_BRSTM:
                        progressText.Text = Properties.Resources.ProgressBrstmBuild;
                        break;
                    default:
                        progressText.Text = $"{Properties.Resources.ProgressDspadpcmEncode} (" + progress + "/" + srcChannelCount + ")";
                        break;
                }
                wavButton.IsEnabled = false;
                brstmButton.IsEnabled = false;
                channelCountSelect.IsEnabled = false;
                finalLapCheckBox.IsEnabled = false;
                loopCheckBox.IsEnabled = false;
                loopStartText.IsEnabled = false;
                loopEndText.IsEnabled = false;
                lpLoadButton.IsEnabled = false;
                lpSaveButton.IsEnabled = false;
            }
        }
        private void wavButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = $@"{Properties.Resources.FiletypeSpecifiedInput} (*.wav;*.wave;*.mp3;*.mp4;*.m4a;*.aac)|*.wav;*.wave;*.mp3;*.mp4;*.m4a;*.aac|すべてのファイル(*.*)|*.*"
            };
            if(dialog.ShowDialog() == true)
            {
                string filePath = dialog.FileName;
                Sound inputWav = new Sound(filePath);
                if (inputWav.error == Sound.NO_ERROR)
                {
                    srcWav = inputWav;
                    //loopEnd = srcWav.sampleLength;
                    wavPathShow.Text = filePath;
                    setUI();
                }
                else
                {
                    MessageBox.Show(Properties.Resources.UnableReadAudioAlert, Properties.Resources.ErrorCapacity,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void brstmButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = $@"{Properties.Resources.FiletypeSpecifiedOutput} (*.brstm)|*.brstm|すべてのファイル (*.*)|*.*"
            };
            if(dialog.ShowDialog() == true)
            {
                brstmOutPath = dialog.FileName;
                brstmPathShow.Text = brstmOutPath;
                setUI();
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            destChannelCount = (uint)channelCountSelect.SelectedIndex + 1;
        }

        private void lpLoadButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = $@"{Properties.Resources.FiletypeLoopSetting} (*.lp)|*.lp|すべてのファイル (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                byte[] src = null;
                try
                {
                    src = File.ReadAllBytes(dialog.FileName);
                }
                catch
                {
                    MessageBox.Show(Properties.Resources.UnableOpenFileAlert, Properties.Resources.ErrorCapacity,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (src.Length != 0x10)
                {
                    MessageBox.Show(Properties.Resources.InvalidFileAlert, Properties.Resources.ErrorCapacity,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (Utils.bytesToUint(src, 0) != 0x504F4F4C || Utils.bytesToUint(src, 4) != 0x1796DD3D)
                {
                    MessageBox.Show(Properties.Resources.InvalidFileAlert, Properties.Resources.ErrorCapacity,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                loopStart = Utils.bytesToUint(src, 8);
                loopEnd = Utils.bytesToUint(src, 0xC);
                setUI();
            }
        }
        private void lpSaveButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = $@"{Properties.Resources.FiletypeLoopSetting} (*.lp)|*.lp|すべてのファイル (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                Utils.uintToBytes(lpFileData, 8, loopStart);
                Utils.uintToBytes(lpFileData, 0xC, loopEnd);
                try
                {
                    File.WriteAllBytes(dialog.FileName, lpFileData);
                }
                catch
                {
                    MessageBox.Show(Properties.Resources.UnableSaveFileAlert, Properties.Resources.ErrorCapacity,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void loopCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (loopCheckBox.IsChecked != null) isLooped = (bool) loopCheckBox.IsChecked;
            setUI();
        }

        private void loopStartText_TextChanged(object sender, TextChangedEventArgs e)
        {
            loopStartText.Text = notIntReg.Replace(loopStartText.Text, "");
            if (loopStartText.Text == "") return;
            try
            {
                loopStart = uint.Parse(loopStartText.Text);
            }
            catch
            {
                MessageBox.Show("Failed to get loop start point", Properties.Resources.ErrorCapacity,
                    MessageBoxButton.OK);
            }
            setUI();
        }

        private void loopStartText_LostFocus(object sender, RoutedEventArgs e)
        {
            if (loopStartText.Text == "") loopStart = 0;
            setUI();
        }
        private void loopEndText_TextChanged(object sender, TextChangedEventArgs e)
        {
            loopEndText.Text = notIntReg.Replace(loopEndText.Text, "");
            if (loopEndText.Text == "")return;
            try
            {
                loopEnd = uint.Parse(loopEndText.Text);
            }
            catch
            {
                MessageBox.Show("Failed to get loop end point", Properties.Resources.ErrorCapacity,
                    MessageBoxButton.OK);
            }
            setUI();
        }

        private void loopEndText_LostFocus(object sender, RoutedEventArgs e)
        {
            if (loopEndText.Text == "") loopEnd = 0;
            setUI();
        }

        private void buildBrstm_Click(object sender, RoutedEventArgs e)
        {
            progress = SPILIT_WAV;
            setUI();
            uint realSampleRate;
            if (finalLap)
            {
                double tmpSampleRate = srcWav.sampleRate * 1.0681;
                tmpSampleRate += 0.5;
                realSampleRate = (uint)tmpSampleRate;
            }
            else
            {
                realSampleRate = srcWav.sampleRate;
            }
            uint loopAutoShift = loopStart % 14336;
            if(loopAutoShift != 0)loopAutoShift = 14336 - loopAutoShift;
            realLoopStart = loopStart + loopAutoShift;
            realLoopEnd = loopEnd + loopAutoShift;
            if (realLoopEnd > srcWav.sampleLength && isLooped)
            {
                var result = MessageBox.Show(Properties.Resources.LoopEndFixCaution, "",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                {
                    progress = NO_PROGRESS;
                    setUI();
                    return;
                }
                realLoopStart = loopStart;
                realLoopEnd = loopEnd;
            }
            try
            {
                Directory.Delete(tmpPath, true);
            }
            catch
            {
                MessageBox.Show("Failed to removed temporary folder", Properties.Resources.ErrorCapacity,
                    MessageBoxButton.OK);
            }
            try
            {
                Directory.CreateDirectory(tmpPath);
            }
            catch
            {
                MessageBox.Show(Properties.Resources.TempFolderFailedAlert, Properties.Resources.ErrorCapacity,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                progress = NO_PROGRESS;
                setUI();
                return;
            }
            Sound[] srcWavs = srcWav.spilitChannel();
            srcChannelCount = srcWav.channelCount;
            if (srcChannelCount > destChannelCount) srcChannelCount = destChannelCount;
            for(int i = 0;i < srcChannelCount; i++)
            {
                srcWavs[i].toPCM16();
                srcWavs[i].sampleRate = realSampleRate;
                if (!srcWavs[i].saveAsWaveFile(tmpPath + i + ".wav"))
                {
                    MessageBox.Show(Properties.Resources.FailedAudioSplitAlert, Properties.Resources.ErrorCapacity,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    progress = NO_PROGRESS;
                    setUI();
                    return;
                }
            }
            progress = 1;
            setUI();
            if (isLooped)
            {
                runCmd("\"" + toolsPath + "DSPADPCM.exe\"", "-e \"" + tmpPath + "0.wav\" \"" + tmpPath + "0.dsp\" -l" + realLoopStart + "-" + realLoopEnd);
            }
            else
            {
                runCmd("\"" + toolsPath + "DSPADPCM.exe\"", "-e \"" + tmpPath + "0.wav\" \"" + tmpPath + "0.dsp\"");
            }
        }
        void runCmd(string exePath, string args)
        {
            ProcessStartInfo cmd = new ProcessStartInfo(exePath, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process process = Process.Start(cmd);
            if (process != null)
            {
                process.EnableRaisingEvents = true;
                process.Exited += cmdExitHandler;
            }
        }
        void cmdExitHandler(object sender, EventArgs e)
        {
            Process process = (Process)sender;
            if (progress == BUILD_BRSTM)
            {
                // Remove generated txt files after built BRSTM
                try
                {
                    for(uint i = 0;i < srcChannelCount;i++) File.Delete(AppDomain.CurrentDomain.BaseDirectory + i + ".txt");
                }
                catch
                {
                    MessageBox.Show("Failed to remove auto-generated txt files", "Error", MessageBoxButton.OK);
                }
                // Set progress status to No Progress after the former processing has been done
                progress = NO_PROGRESS;
                // If brstm file is not exists, display dialog
                if (!File.Exists(tmpPath + "output.brstm"))
                {
                    MessageBox.Show(Properties.Resources.FailedBrstmBuildAlert, Properties.Resources.ErrorCapacity,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    try
                    {
                        File.Delete(brstmOutPath);
                        File.Move(tmpPath + "output.brstm", brstmOutPath);
                        MessageBox.Show(Properties.Resources.BrstmBuildSucceed, "", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch
                    {
                        MessageBox.Show(Properties.Resources.FailedBrstmBuildAlert, Properties.Resources.ErrorCapacity,
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                if (!File.Exists(tmpPath + (progress - 1) + ".dsp"))
                {
                    MessageBox.Show(Properties.Resources.FailedDspadpcmProcessAlert, Properties.Resources.ErrorCapacity,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    progress = NO_PROGRESS;
                }
                else
                {
                    if(progress == srcChannelCount)
                    {
                        progress = BUILD_BRSTM;
                        string wdrevArgs = "--build \"" + tmpPath + "output.brstm\"";
                        for (int i = 0; i < destChannelCount; i++)
                        {
                            wdrevArgs += " \"";
                            wdrevArgs += tmpPath;
                            wdrevArgs += (i % srcChannelCount).ToString();
                            wdrevArgs += ".dsp\"";
                        }
                        runCmd("\"" + toolsPath + "wdrev.exe\"", wdrevArgs);
                    }
                    else
                    {
                        progress++;
                        if (isLooped)
                        {
                            runCmd("\"" + toolsPath + "DSPADPCM.exe\"", "-e \"" + tmpPath + (progress - 1) + ".wav\" \"" + tmpPath + (progress - 1) + ".dsp\" -l" + realLoopStart + "-" + realLoopEnd);
                        }
                        else
                        {
                            runCmd("\"" + toolsPath + "DSPADPCM.exe\"", "-e \"" + tmpPath + (progress - 1) + ".wav\" \"" + tmpPath + (progress - 1) + ".dsp\"");
                        }
                    }
                }
            }
            Dispatcher.Invoke(setUI);
        }

        private void finalLapCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (finalLapCheckBox.IsChecked != null) finalLap = (bool) finalLapCheckBox.IsChecked;
        }
    }
}
