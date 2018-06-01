using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Newtonsoft.Json;

namespace IoTEdgeDeviceSimulator
{
    class Program
    {
        static int counter;
        public static Random random = new Random();

        static void Main(string[] args)
        {
            // The Edge runtime gives us the connection string we need -- it is injected as an environment variable
            string connectionString = Environment.GetEnvironmentVariable("EdgeHubConnectionString");

            // Cert verification is not yet fully functional when using Windows OS for the container
            bool bypassCertVerification = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (!bypassCertVerification) InstallCert();
            Init(connectionString, bypassCertVerification).Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Add certificate in local cert store for use by client for secure connection to IoT Edge runtime
        /// </summary>
        static void InstallCert()
        {
            string certPath = Environment.GetEnvironmentVariable("EdgeModuleCACertificateFile");
            if (string.IsNullOrWhiteSpace(certPath))
            {
                // We cannot proceed further without a proper cert file
                Console.WriteLine($"Missing path to certificate collection file: {certPath}");
                throw new InvalidOperationException("Missing path to certificate file.");
            }
            else if (!File.Exists(certPath))
            {
                // We cannot proceed further without a proper cert file
                Console.WriteLine($"Missing path to certificate collection file: {certPath}");
                throw new InvalidOperationException("Missing certificate file.");
            }
            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            store.Add(new X509Certificate2(X509Certificate2.CreateFromCertFile(certPath)));
            Console.WriteLine("Added Cert: " + certPath);
            store.Close();
        }

        /// <summary>
        /// Initializes the DeviceClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init(string connectionString, bool bypassCertVerification = false)
        {
            Console.WriteLine("Connection String {0}", connectionString);

            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            // During dev you might want to bypass the cert verification. It is highly recommended to verify certs systematically in production
            if (bypassCertVerification)
            {
                mqttSetting.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            }
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            DeviceClient ioTHubModuleClient = DeviceClient.CreateFromConnectionString(connectionString, settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            // Register callback to be called when a message is received by the module
            // await ioTHubModuleClient.SetInputMessageHandlerAsync("input1", PipeMessage, ioTHubModuleClient);

            SendMessages(ioTHubModuleClient);
        }

        static async void SendMessages(object userContext)
        {
            int counterValue = Interlocked.Increment(ref counter);

            var deviceClient = userContext as DeviceClient;
            if (deviceClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            while (true)
            {
                DeviceMessage brMessage = GetSimulatedMessage();
                Microsoft.Azure.Devices.Client.Message message = new Microsoft.Azure.Devices.Client.Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(brMessage)));

                byte[] messageBytes = message.GetBytes();
                string messageString = Encoding.UTF8.GetString(messageBytes);

                if (!string.IsNullOrEmpty(messageString))
                {
                    foreach (var prop in message.Properties)
                    {
                        message.Properties.Add(prop.Key, prop.Value);
                    }
                    await deviceClient.SendEventAsync("output1", message);
                    Console.WriteLine("Received message sent");
                }
                Console.WriteLine(MessageResponse.Completed);
                Thread.Sleep(10000);
            }
        }

        static DeviceMessage GetSimulatedMessage()
        {
            DeviceMessage message = new DeviceMessage();

            try
            {
                message.Name = "Message Name";
                message.TimeStamp = DateTime.Now;
                message.OutputFormat = "JSON";
                message.Message.Log.logDateTime = DateTime.Now;
                message.Message.Log.aFloatValue = (float)random.NextDouble();
                message.Message.Log.aStringValue = "String Value";
                message.Message.Log.anIntValue = random.Next();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            return message;
        }
    }
}