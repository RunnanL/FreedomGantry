using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Text.RegularExpressions;

namespace GantryDllCs
{
    public class Gantry
    {
        SerialPort GantrySerial_clas = new SerialPort();
        public string ReadBuffer;
        public double ratio = 2.0 / 400;
        public int ComStatus = 0;
        private List<string> PosTranslateBuffer = new List<string>();
        public StateMachine States = new StateMachine();

        public string DllVersion()
        {
            return "V1.1.1";
        }

        public struct StateMachine
        {
            public int XPositioningMode;
            public int YPositioningMode;
            public int ZPositioningMode;
        }

        public int GantryComConnect(string ComPort)
        {
            if (1==ComStatus)
            {
                return 2;
            }
            try
            {
                GantrySerial_clas.PortName = ComPort;
                GantrySerial_clas.BaudRate = 115200;
                GantrySerial_clas.DataBits = 8;
                GantrySerial_clas.ReadTimeout = 400;
                GantrySerial_clas.WriteTimeout = 500;
                GantrySerial_clas.StopBits = StopBits.One;
                GantrySerial_clas.DtrEnable = true;
                GantrySerial_clas.Open();
                GantrySerial_clas.DiscardInBuffer();
                GantrySerial_clas.DiscardOutBuffer();
                ComStatus = 1;
                return 1;
            }
            catch (Exception Ex) { return 0; }
        }

        public int GantryComDisconnect()
        {
            try
            {
                GantrySerial_clas.DiscardInBuffer();
                GantrySerial_clas.DiscardOutBuffer();
                GantrySerial_clas.Close();

                if (GantrySerial_clas.IsOpen == false) { ComStatus = 0; return 1; }
                else { return 0; }

            }
            catch (Exception err) { return 0; }
        }

        public int Com_W_R(string Cmd)
        {
            try
            {
                ReadBuffer = string.Empty;
                GantrySerial_clas.DiscardOutBuffer();
                GantrySerial_clas.DiscardInBuffer();
                GantrySerial_clas.WriteLine(Cmd + "\r");
                
                string DataIn = "";
                int Timer = 0;
                while (true)
                {
                    if (Timer > 1500) { return 2; }
                    DataIn = GantrySerial_clas.ReadExisting();
                    if (DataIn.Length > 1 && DataIn != "\r")
                    {
                        ReadBuffer = DataIn;
                        ReadBuffer.Trim();
                        return 1;
                    }
                    else
                    {
                        Thread.Sleep(100);
                        Timer += 100;
                    }
                }
            }
            catch(Exception ErrGantryWR) { return 0; }
        }

        public int MotorCheck(int MotorNo)
        {
            switch(MotorNo)
            {
                case 1:
                    {
                        Com_W_R("#1y1\r");
                        if(ReadBuffer.Length<4)
                        { return 0; }
                        else { return 1; }
                    }
                case 2:
                    {
                        Com_W_R("#2y1\r");
                        if (ReadBuffer.Length < 4)
                        { return 0; }
                        else { return 1; }
                    }
                case 3:
                    {
                        Com_W_R("#3y1\r");
                        if (ReadBuffer.Length < 4)
                        { return 0; }
                        else { return 1; }
                    }
                default:
                    return 2;
            }
        }

        public int AbsCheck(int MotorNo)
        {
            switch (MotorNo)
            {
                case 1:
                    {
                        Com_W_R("#1Z|");
                        if ('2' == ReadBuffer[4])
                        {
                            var Buf1 = ReadBuffer.Split('s');
                            var Buf2 = Buf1[1].Split('u');
                            string Cmd = "#1D" + Buf2[0];
                            Com_W_R(Cmd);
                            States.XPositioningMode = 2;
                            return 1;
                        }
                        else { States.XPositioningMode = 0; return 0; }
                    }
                case 2:
                    {
                        Com_W_R("#2Z|");
                        if ('2' == ReadBuffer[4])
                        {
                            var Buf1 = ReadBuffer.Split('s');
                            var Buf2 = Buf1[1].Split('u');
                            string Cmd = "#2D" + Buf2[0];
                            Com_W_R(Cmd);
                            States.YPositioningMode = 2;
                            return 1;
                        }
                        else { States.YPositioningMode = 0; return 0; }
                    }
                case 3:
                    {
                        Com_W_R("#3Z|");
                        if ('2' == ReadBuffer[4])
                        {
                            var Buf1 = ReadBuffer.Split('s');
                            var Buf2 = Buf1[1].Split('u');
                            string Cmd = "#3D" + Buf2[0];
                            Com_W_R(Cmd);
                            States.ZPositioningMode = 2;
                            return 1;
                        }
                        else { States.ZPositioningMode = 0; return 0; }
                    }
                default:
                    { States.XPositioningMode = 0; States.YPositioningMode = 0; States.ZPositioningMode = 0; return 0; }
            }
        }

        public int MoveGantry(double X, double Y, double Z)
        {
            int Xstp = Convert.ToInt32(X / ratio);
            int Ystp = Convert.ToInt32(Y / ratio);
            int Zstp = Convert.ToInt32(Z / ratio);
            string Cmd = "#1s" + Xstp + "#2s" + Ystp + "#3s" + Zstp;
            Com_W_R(Cmd);
            if(ReadBuffer.Length<1)
            {
                return 0; //Connection loss, no echo
            }
            Com_W_R("#1A#2A#3A");
            if (ReadBuffer.Length < 1)
            {
                return 0; //Connection loss, no echo
            }
            Com_W_R("#1>1#2>1#3>1");
            if (ReadBuffer.Length < 1)
            {
                return 0; //Connection loss, no echo
            }
            else { return 1; }
        }

        public int CtrlReady(ref int ReadyBit)
        {
            ReadyBit = 0;
            Com_W_R("#1$");
            string ReadyBit1 = ReadBuffer;
            Com_W_R("#2$");
            string ReadyBit2 = ReadBuffer;
            Com_W_R("#3$");
            string ReadyBit3 = ReadBuffer;
            if (ReadyBit1.Length>0)
            {
                var Token1 = ReadyBit1.Split('$');
                ReadyBit = ReadyBit + (Convert.ToInt32(Token1[1][2])-'0')*100;
            }
            else { }
            if (ReadyBit2.Length > 0)
            {
                var Token2 = ReadyBit2.Split('$');
                ReadyBit = ReadyBit + (Convert.ToInt32(Token2[1][2]) - '0') * 10;
            }
            else { }
            if (ReadyBit3.Length > 0)
            {
                var Token3 = ReadyBit3.Split('$');
                ReadyBit = ReadyBit + (Convert.ToInt32(Token3[1][2]) - '0') * 1;
            }
            else { }
            return 1;
        }

        public int Pos(ref double X, ref double Y, ref double Z)
        {
            Com_W_R("#1C#2C#3C");
            string CurrentPos = ReadBuffer; //Healthy 3 motor pos read looks like: "1C#2C#3C-5000\r2C#3C-5000+20000\r3C-5000+20000+36000\r"
            PosTranslateBuffer.Clear();
            foreach(Match match in Regex.Matches(CurrentPos, @"[+-][0-9]*"))
            {
                PosTranslateBuffer.Add(match.Value);
            }
            int Count = PosTranslateBuffer.Count;
            switch (Count)
            {
                case 6:
                    {
                        X = Convert.ToDouble(Convert.ToInt32(PosTranslateBuffer[3])*ratio);
                        Y = Convert.ToDouble(Convert.ToInt32(PosTranslateBuffer[4]) * ratio);
                        Z = Convert.ToDouble(Convert.ToInt32(PosTranslateBuffer[5]) * ratio);
                    }
                    return 1;
                case 3: //1 of XYZ motor lose comms
                    { } 
                    return 2;
                case 1: // 2 of XYZ motors loss comms
                    { } 
                    return 3;
                default: //All XYZ motors loss comms or return format incorrect
                    return 0;
            }
        }

        public int GantryStop()
        {
            try
            {
                Com_W_R("#1S1#2S1#3S1"); // Step 1: stop gantry
                //PosRecali(); // Step 2: recalibration controllers at current stop position
            }
            catch (Exception) { return 0; }
            return 1;
        }

        public int PosRecali()
        {
            double Cx, Cy, Cz; // Step 2: recalibration controllers at current stop position
            Cx = 0.0;
            Cy = 0.0;
            Cz = 0.0;
            Pos(ref Cx, ref Cy, ref Cz);
            int Dx = Convert.ToInt32(Cx / ratio);
            int Dy = Convert.ToInt32(Cy / ratio);
            int Dz = Convert.ToInt32(Cz / ratio);
            Com_W_R("#1D" + Dx + "#2D" + Dy + "#3D" + Dz);
            Com_W_R("#1s" + Dx + "#2s" + Dy + "#3s" + Dz);
            Com_W_R("#1>1#2>1#3>1");
            return 1;
        }

        public int RelativeModeXYZ()
        {
            try
            {
                Com_W_R("#1p1");
                if("p+1" == ReadBuffer[1].ToString() + ReadBuffer[2].ToString() + ReadBuffer[3].ToString())
                {
                    States.XPositioningMode = 1;
                }
            }
            catch { return 0; }
            try
            {
                Com_W_R("#2p1");
                if("p+1" == ReadBuffer[1].ToString() + ReadBuffer[2].ToString() + ReadBuffer[3].ToString())
                {
                    States.YPositioningMode = 1;
                }
            }
            catch { return 0; }
            try
            {
                Com_W_R("#3p1");
                if ("p+1" == ReadBuffer[1].ToString() + ReadBuffer[2].ToString() + ReadBuffer[3].ToString())
                {
                    States.ZPositioningMode = 1;
                }
            }
            catch { return 0; }

            return 1;
        }

        public int AbsoluteModeXYZ()
        {
            try
            {
                Com_W_R("#1p2");
                if ("p+2" == ReadBuffer[1].ToString() + ReadBuffer[2].ToString() + ReadBuffer[3].ToString())
                {
                    States.XPositioningMode = 2;
                }
            }
            catch { return 0; }
            try
            {
                Com_W_R("#2p2");
                if ("p+2" == ReadBuffer[1].ToString() + ReadBuffer[2].ToString() + ReadBuffer[3].ToString())
                {
                    States.YPositioningMode = 2;
                }
            }
            catch { return 0; }
            try
            {
                Com_W_R("#3p2");
                if ("p+2" == ReadBuffer[1].ToString() + ReadBuffer[2].ToString() + ReadBuffer[3].ToString())
                {
                    States.ZPositioningMode = 2;
                }
            }
            catch { return 0; }

            return 1;
        }

        public int MoveRelativeX(double DistanceMm)
        {
            int Code = 0;
            int Xstp = Convert.ToInt32(DistanceMm / ratio);
            Code = Com_W_R("#1s"+Xstp);
            if (1 == Code)
            {
                Code = Com_W_R("#1A");
                if (1 == Code)
                {
                    return 1;
                }
                else { return 0; }
            }
            else { return 0; }
        }

        public int MoveRelativeY(double DistanceMm)
        {
            int Code = 0;
            int Ystp = Convert.ToInt32(DistanceMm / ratio);
            Code = Com_W_R("#2s" + Ystp);
            if (1 == Code)
            {
                Code = Com_W_R("#2A");
                if (1 == Code)
                {
                    return 1;
                }
                else { return 0; }
            }
            else { return 0; }
        }

        public int MoveRelativeZ(double DistanceMm)
        {
            int Code = 0;
            int Zstp = Convert.ToInt32(DistanceMm / ratio);
            Code = Com_W_R("#3s" + Zstp);
            if (1 == Code)
            {
                Code = Com_W_R("#3A");
                if (1 == Code)
                {
                    return 1;
                }
                else { return 0; }
            }
            else { return 0; }
        }

        public int Calibrate(double CalX, double CalY, double CalZ)
        {
            try
            {
                int X = Convert.ToInt32(CalX / ratio);
                int Y = Convert.ToInt32(CalY / ratio);
                int Z = Convert.ToInt32(CalZ / ratio);
                int Code = 0;

                Code = Com_W_R("#1D" + X.ToString());
                if (0 == Code) { return 0; }
                Code = Com_W_R("#1s" + X.ToString());
                if (0 == Code) { return 0; }
                Code = Com_W_R("#1p2" + X.ToString());
                if (0 == Code) { return 0; }
                Code = Com_W_R("#1>1");
                if (0 == Code) { return 0; }

                Code = Com_W_R("#2D" + Y.ToString());
                if (0 == Code) { return 0; }
                Code = Com_W_R("#2s" + Y.ToString());
                if (0 == Code) { return 0; }
                Code = Com_W_R("#2p2" + X.ToString());
                if (0 == Code) { return 0; }
                Code = Com_W_R("#2>1");
                if (0 == Code) { return 0; }

                Code = Com_W_R("#3D" + Z.ToString());
                if (0 == Code) { return 0; }
                Code = Com_W_R("#3s" + Z.ToString());
                if (0 == Code) { return 0; }
                Code = Com_W_R("#3p2" + X.ToString());
                if (0 == Code) { return 0; }
                Code = Com_W_R("#3>1");
                if (0 == Code) { return 0; }

                return 1;
            }
            catch { return 0; }
        }
    }
}
