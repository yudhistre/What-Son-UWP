using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System.Diagnostics;
using What_Son__UWP_;

namespace What_Son__UWP_
{
    class IoTHubConnector
    {
        static DeviceClient deviceClient;
        static string iotHubUri = "demomiami.azure-devices.net";
        static string deviceKey = "MmLY+KA5AVsp4h2ubiF87/EFt9ujDHRoA3R8So4/hWc=";

        public static async void SendDeviceToCloudMessagesAsync(string text)
        {
            deviceClient = DeviceClient.Create(iotHubUri, new DeviceAuthenticationWithRegistrySymmetricKey("whatsonDevice", deviceKey));
            var telemetryDataPoint = new
            {
                deviceId = "whatsonDevice",
                question = text
            };
            var messageString = JsonConvert.SerializeObject(telemetryDataPoint);
            var message = new Message(Encoding.ASCII.GetBytes(messageString));

            Debug.WriteLine("Simulated device: Text " + message);
            await deviceClient.SendEventAsync(message);

            Debug.WriteLine("{0} > Sending message: {1}", DateTime.Now, messageString);
            ReceiveC2dAsync();
        }

        private static async void ReceiveC2dAsync()
        {
            while (true)
            {
                Message receivedMessage = await deviceClient.ReceiveAsync();
                if (receivedMessage == null) continue;
                var msg = Encoding.ASCII.GetString(receivedMessage.GetBytes());
                MainPage.readMessage(msg);
                await deviceClient.CompleteAsync(receivedMessage);
            }
        }
    }
}
