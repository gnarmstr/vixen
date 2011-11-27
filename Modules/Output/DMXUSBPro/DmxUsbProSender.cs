namespace VixenModules.Output.DmxUsbPro
{
    using System;
    using System.IO.Ports;

    using Vixen.Commands;

    internal class DmxUsbProSender : IDisposable
    {
        private readonly Message _dmxPacketMessage;

        private readonly byte[] _statePacket;

        private SerialPort _serialPort;

        public DmxUsbProSender(SerialPort serialPort)
        {
            this._serialPort = serialPort;
            this._statePacket = new byte[513];
            this._dmxPacketMessage = new Message(MessageType.OutputOnlySendDMXPacketRequest)
                {
                    Data = this._statePacket
                };
        }

        ~DmxUsbProSender()
        {
            this.Dispose();
        }

        public void SendDmxPacket(Command[] outputStates)
        {
            if (outputStates == null || this._statePacket == null || _serialPort == null)
            {
                return;
            }

            var channelValues = new byte[outputStates.Length];
            for (int index = 0; index < outputStates.Length; index++)
            {
                Command command = outputStates[index];
                if (command == null)
                {
                    // State reset
                    channelValues[index] = 0;
                    continue;
                }

                // Casting is fasting than comparing strings.
                var setLevelCommand = command as Lighting.Monochrome.SetLevel;
                if (setLevelCommand != null)
                {
                    // Good command
                    var level = (byte)(0xFF * setLevelCommand.Level / 100);
                    channelValues[index] = level;
                }                
            }

            if (!this._serialPort.IsOpen)
            {
                this._serialPort.Open();
            }

            this._statePacket[0] = 0; // Start code
            Array.Copy(channelValues, 0, this._statePacket, 1, Math.Min(512, channelValues.Length));
            byte[] packet = this._dmxPacketMessage.Packet;
            if (packet != null)
            {
                this._serialPort.Write(packet, 0, packet.Length);
            }
        }

        public void Start()
        {
            if (_serialPort != null && !this._serialPort.IsOpen)
            {
                this._serialPort.Open();
            }
        }

        public void Stop()
        {
            if (_serialPort != null && this._serialPort.IsOpen)
            {
                this._serialPort.Close();
            }
        }

        public void Dispose()
        {
            if (this._serialPort != null && this._serialPort.IsOpen)
            {
                this._serialPort.Close();
                this._serialPort.Dispose();
                this._serialPort = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}