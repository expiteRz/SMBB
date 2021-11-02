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
        public static byte[] byteArrayCut(byte[] src, uint offset, uint length)
        {
            byte[] dest = new byte[length];
            Array.Copy(src, offset, dest, 0, length);
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
        public static uint bytesToUint(byte[] data, uint offset, bool isLE)
        {
            uint dest = 0;
            for (int i = 0; i < 4; i++)
            {
                dest <<= 8;
                if (isLE)
                {
                    dest += data[offset + (3 - i)];
                }
                else
                {
                    dest += data[offset + i];
                }
            }
            return dest;
        }
        public static uint bytesToUint(byte[] data, uint offset)
        {
            return bytesToUint(data, offset, true);
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
        public static ushort bytesToUshort(byte[] data, uint offset, bool isLE)
        {
            ushort dest = 0;
            for (int i = 0; i < 2; i++)
            {
                dest <<= 8;
                if (isLE)
                {
                    dest += data[offset + (1 - i)];
                }
                else
                {
                    dest += data[offset + i];
                }
            }
            return dest;
        }
        public static ushort bytesToUshort(byte[] data, uint offset)
        {
            return bytesToUshort(data, offset, true);
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
        public static short clamp16(int val)
        {
            if (val > short.MaxValue) return short.MaxValue;
            if (val < short.MinValue) return short.MinValue;
            return (short)val;
        }
    }
    class Sound
    {
        public const string RIFF_TAG = "RIFF";
        public const string WAVE_TAG = "WAVE";
        public const string DATA_TAG = "data";
        public const string FMT_TAG = "fmt ";
        public const string SMPL_TAG = "smpl";
        public const ushort PCM_8 = 0;
        public const ushort PCM_16 = 1;
        public const ushort PCM_24 = 65534;
        public const ushort PCM_32 = 65533;
        public const ushort PCM_64 = 65532;
        public const string BSTM_RSTM_TAG = "RSTM";
        public const string BSTM_CSTM_TAG = "CSTM";
        public const string BSTM_FSTM_TAG = "FSTM";
        public const string BSTM_HEAD_TAG = "HEAD";
        public const string BSTM_INFO_TAG = "INFO";
        public const string BSTM_DATA_TAG = "DATA";
        public const byte BSTM_PCM_8 = 0;
        public const byte BSTM_PCM_16 = 1;
        public const byte BSTM_DSP_ADPCM = 2;
        public const byte BSTM_IMA_ADPCM = 3;
        //public const ushort MS_ADPCM = 2;
        public const ushort PCM_FLOAT = 3;
        public const byte NO_ERROR = 0;
        public const byte ERROR_OPEN_FILE = 1;
        public const byte ERROR_INVALID_FILE = 2;
        public const byte ERROR_INVALID_ARGS = 3;
        public byte error = ERROR_INVALID_ARGS;
        public uint sampleRate;
        public uint sampleLength;
        public bool isLooped = false;
        public uint loopStart = 0;
        public uint loopEnd = 0;
        public ushort channelCount;
        public ushort format;
        public byte[] data = null;
        public Sound(){}
        public Sound(uint _sampleRate, uint _sampleLength, ushort _channelCount, ushort _format, bool _isLooped, uint _loopStart, uint _loopEnd)
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
            isLooped = _isLooped;
            loopStart = _loopStart;
            loopEnd = _loopEnd;
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
                readBrstmFromBytes(src);
            }
            catch
            {

            }
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
                else if (curElementName == SMPL_TAG)
                {
                    if (curElementSize >= 0x3C)
                    {
                        if(Utils.bytesToUint(src, curElementOffset + 0x24) != 0 && Utils.bytesToUint(src, curElementOffset + 0x30) == 0)
                        {
                            isLooped = true;
                            loopStart = Utils.bytesToUint(src, curElementOffset + 0x34);
                            loopEnd = Utils.bytesToUint(src, curElementOffset + 0x38);
                        }
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
        void readBrstmFromBytes(byte[] src)
        {
            bool isLE;
            bool isBcstm = false;
            if (Utils.bytesToUshort(src, 4) == 0xFFFE)
            {
                isLE = false;
            }
            else if(Utils.bytesToUshort(src, 4) == 0xFEFF)
            {
                isLE = true;
            }else{
                return;
            }
            if (Utils.bytesToString(src, 0, 4) != BSTM_RSTM_TAG)
            {
                if (Utils.bytesToString(src, 0, 4) == BSTM_CSTM_TAG || Utils.bytesToString(src, 0, 4) == BSTM_FSTM_TAG)
                {
                    isBcstm = true;
                }
                else
                {
                    return;
                }
            }
            uint headOffset = 0;
            uint dataOffset = 0;
            uint dataSize = 0;
            if (isBcstm)
            {
                uint chunkCount = Utils.bytesToUshort(src, 0x10, isLE);
                uint curChunk = 0x14;
                if (chunkCount != 2 && chunkCount != 3) return;
                for (uint i = 0; i < chunkCount; i++)
                {
                    ushort curChunkTag = Utils.bytesToUshort(src, curChunk, isLE);
                    switch (curChunkTag)
                    {
                        case 0x4000:
                            headOffset = Utils.bytesToUint(src, curChunk + 4, isLE);
                            break;
                        case 0x4001:
                            break;
                        case 0x4002:
                            dataOffset = Utils.bytesToUint(src, curChunk + 4, isLE);
                            dataSize = Utils.bytesToUint(src, curChunk + 8, isLE);
                            break;
                        default:
                            return;
                    }
                    curChunk += 0xC;
                }
            }
            else
            {
                headOffset = Utils.bytesToUint(src, 0x10, isLE);
                dataOffset = Utils.bytesToUint(src, 0x20, isLE);
                dataSize = Utils.bytesToUint(src, 0x24, isLE);
            }
            if (Utils.bytesToString(src, headOffset, 4) != BSTM_HEAD_TAG && Utils.bytesToString(src, headOffset, 4) != BSTM_INFO_TAG) return;
            if (Utils.bytesToString(src, dataOffset, 4) != BSTM_DATA_TAG) return;
            uint head1Offset = headOffset + 8 + Utils.bytesToUint(src, headOffset + 0xC, isLE);
            byte brstmFormat = src[head1Offset];
            channelCount = src[head1Offset + 2];
            if(isBcstm){
                sampleRate = Utils.bytesToUint(src, head1Offset + 4, isLE);
            }else{
                sampleRate = Utils.bytesToUshort(src, head1Offset + 4, isLE);
            }
            sampleLength = Utils.bytesToUint(src, head1Offset + 0xC, isLE);
            if (src[head1Offset + 1] != 0)
            {
                isLooped = true;
                loopStart = Utils.bytesToUint(src, head1Offset + 8, isLE);
                loopEnd = sampleLength - 1;
            }
            uint blockCount;
            uint blockSize;
            uint lastBlockSize;
            uint lastBlockSizeWithPad;
            uint dataPadding;
            if (isBcstm)
            {
                blockCount = Utils.bytesToUint(src, head1Offset + 0x10, isLE);
                blockSize = Utils.bytesToUint(src, head1Offset + 0x14, isLE);
                lastBlockSize = Utils.bytesToUint(src, head1Offset + 0x1C, isLE);
                lastBlockSizeWithPad = Utils.bytesToUint(src, head1Offset + 0x24, isLE);
                dataPadding = Utils.bytesToUint(src, head1Offset + 0x34, isLE);
            }
            else
            {
                blockCount = Utils.bytesToUint(src, head1Offset + 0x14, isLE);
                blockSize = Utils.bytesToUint(src, head1Offset + 0x18, isLE);
                lastBlockSize = Utils.bytesToUint(src, head1Offset + 0x20, isLE);
                lastBlockSizeWithPad = Utils.bytesToUint(src, head1Offset + 0x28, isLE);
                dataPadding = Utils.bytesToUint(src, dataOffset + 8, isLE);
            }
            DspAdpcmInfo[] infos = new DspAdpcmInfo[channelCount];
            uint head3Offset = headOffset + 8 + Utils.bytesToUint(src, headOffset + 0x1C, isLE);
            if (brstmFormat == BSTM_DSP_ADPCM)
            {
                for (uint i = 0; i < channelCount; i++)
                {
                    uint channelInfoOffset = 0;
                    if (isBcstm)
                    {
                        channelInfoOffset = head3Offset + Utils.bytesToUint(src, head3Offset + 8 * (i + 1), isLE);
                        channelInfoOffset = channelInfoOffset + Utils.bytesToUint(src, channelInfoOffset + 4, isLE);
                    }
                    else
                    {
                        channelInfoOffset = headOffset + 8 + Utils.bytesToUint(src, head3Offset + 8 * (i + 1), isLE);
                        channelInfoOffset = headOffset + 8 + Utils.bytesToUint(src, channelInfoOffset + 4, isLE);
                    }
                    infos[i] = new DspAdpcmInfo(Utils.byteArrayCut(src, channelInfoOffset, 0x28), isLE);
                    infos[i].sampleLength = sampleLength;
                }
            }
            switch (brstmFormat)
            {
                case BSTM_PCM_8:
                    format = PCM_8;
                    return;
                    //break;
                    //現時点では対応しない
                case BSTM_PCM_16:
                case BSTM_DSP_ADPCM:
                    format = PCM_16;
                    break;
                default:
                    return;

            }
            data = new byte[channelCount * sampleLength * (getBps() / 8)];
            byte[][] spilitedData = spilitBrstmDataByChannel(Utils.byteArrayCut(src, dataOffset + 8 + dataPadding, dataSize - (dataPadding + 8)), blockCount, blockSize, lastBlockSize, lastBlockSizeWithPad, channelCount);
            for (uint i = 0; i < channelCount; i++)
            {
                if (brstmFormat == BSTM_DSP_ADPCM)
                {
                    short[] rawPcm16 = DspAdpcmDecoder.decode(spilitedData[i], infos[i]);
                    for (uint j = 0; j < sampleLength; j++) Utils.ushortToBytes(data, (channelCount * j + i) * 2, (ushort)rawPcm16[j]);
                }
                else if(brstmFormat == BSTM_PCM_16)
                {
                    for (uint j = 0; j < sampleLength; j++) {
                        ushort tmpPcm16 = Utils.bytesToUshort(spilitedData[i], j * 2, isLE);
                        Utils.ushortToBytes(data, (channelCount * j + i) * 2, tmpPcm16);
                    }
                }
                else
                {
                    //for (uint j = 0; j < sampleLength; j++) data[j * channelCount + i] = rawData[i][j];
                }
            }
            error = NO_ERROR;
        }
        static byte[][] spilitBrstmDataByChannel(byte[] data, uint blockCount, uint blockSize, uint lastBlockSize, uint lastBlockSizeWithPad, uint channelCount)
        {
            byte[][] dest = new byte[channelCount][];
            for (uint i = 0; i < channelCount; i++)
            {
                dest[i] = new byte[(blockCount - 1) * blockSize + lastBlockSize];
                for (uint j = 0; j < blockCount; j++)
                {
                    if (j < blockCount - 1)
                    {
                        Utils.memcpy(dest[i], j * blockSize, data, blockSize * (channelCount * j + i), blockSize);
                    }
                    else
                    {
                        Utils.memcpy(dest[i], j * blockSize, data, blockSize * (channelCount * (blockCount - 1)) + lastBlockSizeWithPad * i, lastBlockSize);
                    }
                }
            }
            return dest;
        }
        public Sound[] spilitChannel()
        {
            Sound[] dest = new Sound[channelCount];
            for (uint i = 0; i < channelCount; i++)
            {
                dest[i] = new Sound(sampleRate, sampleLength, 1, format, isLooped, loopStart, loopEnd);
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
        uint sampleToTime(uint sample)
        {
            double time = sample / (double)sampleRate;
            time *= 1000;
            time += 0.5;
            return (uint)time;
        }
        uint timeToSample(uint time)
        {
            double sample = ((double)time / 1000) * sampleRate;
            sample += 0.5;
            return (uint)sample;
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
        public void changeSampleLength(uint _sampleLength)
        {
            if (format == PCM_8 || format == PCM_16 || format == PCM_24 || format == PCM_32 || format == PCM_64 || format == PCM_FLOAT)
            {
                Array.Resize(ref data, (int)(_sampleLength * channelCount * (getBps() / 8)));
                sampleLength = _sampleLength;
            }
        }
        public void sampleCopy(uint destIndex, uint srcIndex, uint length)
        {
            if ((destIndex + length) > sampleLength) changeSampleLength(destIndex + length);
            if (format == PCM_8 || format == PCM_16 || format == PCM_24 || format == PCM_32 || format == PCM_64 || format == PCM_FLOAT)
            {
                uint byteSrcIndex = srcIndex * channelCount * (getBps() / 8);
                uint byteDestIndex = destIndex * channelCount * (getBps() / 8);
                uint byteLength = length * channelCount * (getBps() / 8);
                Utils.memcpy(data, byteDestIndex, data, byteSrcIndex, byteLength);
            }
        }
        public void adjustLoop()
        {
            if (!isLooped) return;
            uint loopAutoShift = loopStart % 14336;
            if (loopAutoShift != 0) loopAutoShift = 14336 - loopAutoShift;
            if ((loopEnd - loopStart + 1) >= loopAutoShift)
            {
                sampleCopy(loopEnd + 1, loopStart, loopAutoShift);
                loopStart += loopAutoShift;
                loopEnd += loopAutoShift;
            }

        }
        public void deleteOutro()
        {
            if(isLooped) changeSampleLength(loopEnd + 1);
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
    class DspAdpcmInfo
    {
        public uint sampleLength;
        public short[] coefs = new short[16];
        public short predScale;
        public short hist1;
        public short hist2;
        public DspAdpcmInfo(byte[] src, bool isLE)
        {
            for (uint i = 0; i < 16; i++) coefs[i] = (short)Utils.bytesToUshort(src, i * 2, isLE);
            predScale = (short)Utils.bytesToUshort(src, 0x22, isLE);
            hist1 = (short)Utils.bytesToUshort(src, 0x24, isLE);
            hist2 = (short)Utils.bytesToUshort(src, 0x26, isLE);
        }
    }
    class DspAdpcmDecoder
    {
        public const int SAMPLES_PER_FRAME = 14;
        public static short[] decode(byte[] src, DspAdpcmInfo info)
        {
            uint samplesRemaining = info.sampleLength;
            uint srcIndex = 0;
            uint destIndex = 0;
            short[] dest = new short[info.sampleLength];
            short hist1 = info.hist1;
            short hist2 = info.hist2;
            uint frameCount = (info.sampleLength + SAMPLES_PER_FRAME - 1) / SAMPLES_PER_FRAME;
            for (uint i = 0; i < frameCount; i++)
            {
                int predictor = src[srcIndex] >> 4;
                int scale = 1 << (src[srcIndex] & 0xF);
                srcIndex++;
                short coef1 = info.coefs[predictor * 2];
                short coef2 = info.coefs[predictor * 2 + 1];
                uint samplesInFrame = SAMPLES_PER_FRAME;
                if (samplesRemaining < SAMPLES_PER_FRAME) samplesInFrame = samplesRemaining;
                for (uint j = 0; j < samplesInFrame; j++)
                {
                    int curSample;
                    if (j % 2 == 0)
                    {
                        curSample = src[srcIndex] >> 4;
                    }
                    else
                    {
                        curSample = src[srcIndex] & 0xF;
                        srcIndex++;
                    }
                    if (curSample >= 8) curSample -= 16;
                    curSample = (((scale * curSample) << 11) + 1024 + (coef1 * hist1 + coef2 * hist2)) >> 11;
                    short curRealSample = Utils.clamp16(curSample);
                    hist2 = hist1;
                    hist1 = curRealSample;
                    dest[destIndex] = curRealSample;
                    destIndex++;
                }
                samplesRemaining -= samplesInFrame;
            }
            return dest;
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
        const int DECODE_SOUND = -3;
        int progress = NO_PROGRESS;
        Regex notIntReg = new Regex(@"[^0-9]");
        string[] needToolFiles = {"DSPADPCM.exe", "dsptool.dll", "hio2.dll", "soundfile.dll", "wdrev.exe" };
        string toolsPath;
        string tmpPath;
        Sound srcWav = new Sound();
        byte[] lpFileData = {0x4C, 0x4F, 0x4F, 0x50, 0x3D, 0xDD, 0x96, 0x17, 0, 0, 0, 0, 0, 0, 0, 0};
        bool finalLap = false;
        uint realLoopStart;
        uint realLoopEnd;
        uint srcChannelCount;
        uint destChannelCount = 2;
        string wavInputPath = "";
        string brstmOutPath = "";
        public MainWindow()
        {
            InitializeComponent();
            toolsPath = AppDomain.CurrentDomain.BaseDirectory + "tools\\";
            tmpPath = AppDomain.CurrentDomain.BaseDirectory + "tmp\\";
            bool needToolFilesOK = true;
            string needToolFilesErrMsg = "必要なファイルが不足しています。以下のファイルを\"tools\"フォルダーに入れてください\n";
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
                MessageBox.Show(needToolFilesErrMsg, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }
        private void setUI()
        {
            if (buildBrstm == null) return;
            buildBrstm.IsEnabled = true;
            wavPathShow.Text = wavInputPath;
            brstmPathShow.Text = brstmOutPath;
            sampleWarnText.Text = "";
            if (srcWav.isLooped)
            {
                if (srcWav.loopStart >= srcWav.loopEnd)
                {
                    sampleWarnText.Text = "ループ終了をループ開始より後にしてください";
                    buildBrstm.IsEnabled = false;
                }
                if (srcWav.error == Sound.NO_ERROR && srcWav.loopEnd >= srcWav.sampleLength)
                {
                    sampleWarnText.Text = "ループ終了を" + (srcWav.sampleLength - 1).ToString() + "以下にしてください";
                    buildBrstm.IsEnabled = false;
                }
                loopCheckBox.IsChecked = true;
                loopStartText.IsEnabled = true;
                loopEndText.IsEnabled = true;
                lpLoadButton.IsEnabled = true;
                lpSaveButton.IsEnabled = true;
            }
            else
            {
                loopCheckBox.IsChecked = false;
                loopStartText.IsEnabled = false;
                loopEndText.IsEnabled = false;
                lpLoadButton.IsEnabled = false;
                lpSaveButton.IsEnabled = false;
            }
            loopStartText.Text = srcWav.loopStart.ToString();
            loopEndText.Text = srcWav.loopEnd.ToString();
            if(progress == NO_PROGRESS)
            {
                progressText.Text = "";
                wavButton.IsEnabled = true;
                brstmButton.IsEnabled = true;
                channelCountSelect.IsEnabled = true;
                finalLapCheckBox.IsEnabled = true;
                loopCheckBox.IsEnabled = true;
                if (brstmOutPath == "" || srcWav.error != Sound.NO_ERROR) buildBrstm.IsEnabled = false;
            }
            else
            {
                switch (progress)
                {
                    case SPILIT_WAV:
                        progressText.Text = "進捗:音声ファイル分割中";
                        break;
                    case BUILD_BRSTM:
                        progressText.Text = "進捗:BRSTM作成中";
                        break;
                    case DECODE_SOUND:
                        wavPathShow.Text = "しばらくお待ちください...";
                        break;
                    default:
                        progressText.Text = "進捗:DSPADPCMエンコード中 (" + progress.ToString() + "/" + srcChannelCount.ToString() + ")";
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
                buildBrstm.IsEnabled = false;
            }
        }
        private void wavButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "音声ファイル (*.wav;*.wave;*.mp3;*.mp4;*.m4a;*.aac;*.brstm;*.bcstm;*.bfstm)|*.wav;*.wave;*.mp3;*.mp4;*.m4a;*.aac;*.brstm;*.bcstm;*.bfstm|すべてのファイル(*.*)|*.*";
            if(dialog.ShowDialog() == true)
            {
                progress = DECODE_SOUND;
                setUI();
                Task.Run(() => decodeSound(dialog.FileName));
            }
        }
        void decodeSound(string filePath)
        {
            Sound inputWav = new Sound(filePath);
            if (inputWav.error == Sound.NO_ERROR)
            {
                srcWav = inputWav;
                wavInputPath = filePath;
            }
            else
            {
                this.Dispatcher.Invoke((Action)(() =>
                {
                    MessageBox.Show("無効な音声ファイル、もしくはサポートされていないフォーマットです", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }));
            }
            progress = NO_PROGRESS;
            this.Dispatcher.Invoke((Action)(() =>
            {
                setUI();
            }));
        }
        private void brstmButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog();
            dialog.Filter = "BRSTMファイル (*.brstm)|*.brstm|すべてのファイル (*.*)|*.*";
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
            var dialog = new OpenFileDialog();
            dialog.Filter = "ループ設定ファイル (*.lp)|*.lp|すべてのファイル (*.*)|*.*";
            if (dialog.ShowDialog() == true)
            {
                byte[] src = null;
                try
                {
                    src = File.ReadAllBytes(dialog.FileName);
                }
                catch
                {
                    MessageBox.Show("エラーが発生したためファイルを開けませんでした", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (src.Length != 0x10)
                {
                    MessageBox.Show("無効なファイルです。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (Utils.bytesToUint(src, 0) != 0x504F4F4C || Utils.bytesToUint(src, 4) != 0x1796DD3D)
                {
                    MessageBox.Show("無効なファイルです。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                srcWav.loopStart = Utils.bytesToUint(src, 8);
                srcWav.loopEnd = Utils.bytesToUint(src, 0xC);
                setUI();
            }
        }
        private void lpSaveButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog();
            dialog.Filter = "ループ設定ファイル (*.lp)|*.lp|すべてのファイル (*.*)|*.*";
            if (dialog.ShowDialog() == true)
            {
                Utils.uintToBytes(lpFileData, 8, srcWav.loopStart);
                Utils.uintToBytes(lpFileData, 0xC, srcWav.loopEnd);
                try
                {
                    File.WriteAllBytes(dialog.FileName, lpFileData);
                }
                catch
                {
                    MessageBox.Show("エラーが発生したためファイルを保存できませんでした", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void loopCheckBox_Click(object sender, RoutedEventArgs e)
        {
            srcWav.isLooped = (bool)loopCheckBox.IsChecked;
            setUI();
        }

        private void loopStartText_TextChanged(object sender, TextChangedEventArgs e)
        {
            loopStartText.Text = notIntReg.Replace(loopStartText.Text, "");
            if (loopStartText.Text == "") return;
            try
            {
                srcWav.loopStart = uint.Parse(loopStartText.Text);
            }
            catch
            {

            }
            setUI();
        }

        private void loopStartText_LostFocus(object sender, RoutedEventArgs e)
        {
            if (loopStartText.Text == "") srcWav.loopStart = 0;
            setUI();
        }
        private void loopEndText_TextChanged(object sender, TextChangedEventArgs e)
        {
            loopEndText.Text = notIntReg.Replace(loopEndText.Text, "");
            if (loopEndText.Text == "")return;
            try
            {
                srcWav.loopEnd = uint.Parse(loopEndText.Text);
            }
            catch
            {

            }
            setUI();
        }

        private void loopEndText_LostFocus(object sender, RoutedEventArgs e)
        {
            if (loopEndText.Text == "") srcWav.loopEnd = 0;
            setUI();
        }

        private void buildBrstm_Click(object sender, RoutedEventArgs e)
        {
            progress = SPILIT_WAV;
            setUI();
            try
            {
                Directory.Delete(tmpPath, true);
            }
            catch
            {

            }
            try
            {
                Directory.CreateDirectory(tmpPath);
            }
            catch
            {
                MessageBox.Show("tmpフォルダーの作成に失敗しました", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                progress = NO_PROGRESS;
                setUI();
                return;
            }
            Task.Run(() => spilitWav());
        }
        void spilitWav()
        {
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
            Sound[] srcWavs = srcWav.spilitChannel();
            srcChannelCount = srcWav.channelCount;
            if (srcChannelCount > destChannelCount) srcChannelCount = destChannelCount;
            for (int i = 0; i < srcChannelCount; i++)
            {
                srcWavs[i].toPCM16();
                srcWavs[i].sampleRate = realSampleRate;
                srcWavs[i].adjustLoop();
                srcWavs[i].deleteOutro();
                if (!srcWavs[i].saveAsWaveFile(tmpPath + i.ToString() + ".wav"))
                {
                    MessageBox.Show("音声ファイル分割中にエラーが発生しました", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    progress = NO_PROGRESS;
                    this.Dispatcher.Invoke((Action)(() =>
                    {
                        setUI();
                    }));
                    return;
                }
            }
            realLoopStart = srcWavs[0].loopStart;
            realLoopEnd = srcWavs[0].loopEnd;
            progress = 1;
            this.Dispatcher.Invoke((Action)(() =>
            {
                setUI();
            }));
            if (srcWav.isLooped)
            {
                runCmd("\"" + toolsPath + "DSPADPCM.exe\"", "-e \"" + tmpPath + "0.wav\" \"" + tmpPath + "0.dsp\" -l" + realLoopStart.ToString() + "-" + realLoopEnd.ToString());
            }
            else
            {
                runCmd("\"" + toolsPath + "DSPADPCM.exe\"", "-e \"" + tmpPath + "0.wav\" \"" + tmpPath + "0.dsp\"");
            }
        }
        void runCmd(string exePath, string args)
        {
            ProcessStartInfo cmd = new ProcessStartInfo(exePath, args);
            cmd.CreateNoWindow = true;
            cmd.UseShellExecute = false;
            Process process = Process.Start(cmd);
            process.EnableRaisingEvents = true;
            process.Exited += new EventHandler(cmdExitHandler);
        }
        void cmdExitHandler(object sender, EventArgs e)
        {
            Process process = (Process)sender;
            if (progress == BUILD_BRSTM)
            {
                try
                {
                    for(uint i = 0;i < srcChannelCount;i++) File.Delete(AppDomain.CurrentDomain.BaseDirectory + i.ToString() + ".txt");
                }
                catch
                {

                }
                progress = NO_PROGRESS;
                if (!File.Exists(tmpPath + "output.brstm"))
                {
                    MessageBox.Show("不明なエラーが発生したため、BRSTMを作成できませんでした", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    try
                    {
                        File.Delete(brstmOutPath);
                        File.Move(tmpPath + "output.brstm", brstmOutPath);
                        MessageBox.Show("BRSTMは正常に作成されました", "", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch
                    {
                        MessageBox.Show("不明なエラーが発生したため、BRSTMを作成できませんでした", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                if (!File.Exists(tmpPath + (progress - 1).ToString() + ".dsp"))
                {
                    MessageBox.Show("\"DSPADPCM.exe\"でエラーが発生しました", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        if (srcWav.isLooped)
                        {
                            runCmd("\"" + toolsPath + "DSPADPCM.exe\"", "-e \"" + tmpPath + (progress - 1).ToString() + ".wav\" \"" + tmpPath + (progress - 1).ToString() + ".dsp\" -l" + realLoopStart.ToString() + "-" + realLoopEnd.ToString());
                        }
                        else
                        {
                            runCmd("\"" + toolsPath + "DSPADPCM.exe\"", "-e \"" + tmpPath + (progress - 1).ToString() + ".wav\" \"" + tmpPath + (progress - 1).ToString() + ".dsp\"");
                        }
                    }
                }
            }
            this.Dispatcher.Invoke((Action)(() =>
            {
                setUI();
            }));
        }

        private void finalLapCheckBox_Click(object sender, RoutedEventArgs e)
        {
            finalLap = (bool)finalLapCheckBox.IsChecked;
        }
    }
}
