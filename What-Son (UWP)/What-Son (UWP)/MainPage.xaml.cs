using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.SpeechSynthesis;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Media.SpeechRecognition;
using Windows.UI.Core;
using Windows.ApplicationModel.Resources.Core;
using Windows.Storage;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace What_Son__UWP_
{

    public sealed partial class MainPage : Page
    {
        private I2cDevice Device;
        private Timer periodicTimer;
        private DispatcherTimer timer;
        private DispatcherTimer PauseForNewExpression;
        private DispatcherTimer BlinkTimer;
        //private Timer sTimer;
        private bool blink;
        static MediaElement mediaElement;
        private static MediaState state = MediaState.Stopped;

        //private static uint HResultPrivacyStatementDeclined = 0x80045509;
        private static SpeechRecognizer speechRecognizer;
        private static SpeechSynthesizer synth = new SpeechSynthesizer();
        //private IAsyncOperation<SpeechRecognitionResult> recognitionOperation;

        // Tag TARGET
        private const string TAG_TARGET = "target";
        // Tag CMD
        private const string TAG_CMD = "cmd";
        // Tag Device
        private const string TAG_DEVICE = "device";

        // Grammer File
        private const string SRGS_FILE = "Grammar\\grammar.xml";

        private async void initcomunica()

        {

            var settings = new I2cConnectionSettings(0x40); // Arduino address

            settings.BusSpeed = I2cBusSpeed.StandardMode;

            string aqs = I2cDevice.GetDeviceSelector("I2C1");

            var dis = await DeviceInformation.FindAllAsync(aqs);

            Device = await I2cDevice.FromIdAsync(dis[0].Id, settings);

            periodicTimer = new Timer(this.TimerCallback, null, 0, 100); // Create a timmer

        }

        private void TimerCallback(object state)

        {
            byte[] RegAddrBuf = new byte[] { 0x40 };
            byte[] ReadBuf = new byte[1];

            try
            {
                Device.Read(ReadBuf); // read the data
            }

            catch (Exception f)
            {
                Debug.WriteLine(f.Message);
            }

            char[] cArray = System.Text.Encoding.UTF8.GetString(ReadBuf, 0, 1).ToCharArray();  // Converte  Byte to Char
            String c = new String(cArray);
            Debug.WriteLine(c);

            //talk 

        }


        public MainPage()
        {
            this.InitializeComponent();

            initializeSpeechRecognizer();

            stateNormal();
            mediaElement = new MediaElement();

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(135);
            timer.Tick += TimerCallback;

            PauseForNewExpression = new DispatcherTimer();
            PauseForNewExpression.Interval = TimeSpan.FromMilliseconds(6000);
            PauseForNewExpression.Tick += newExpression;

            BlinkTimer = new DispatcherTimer();
            BlinkTimer.Interval = TimeSpan.FromMilliseconds(3800);
            BlinkTimer.Tick += startBlink;
            BlinkTimer.Start();

            mediaElement.MediaEnded += MediaElement_MediaEnded;

            // Function for controlling Arduino from RPi
            //initcomunica();
        }

        private void MediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            state = MediaState.Stopped;
        }

        private async void initializeSpeechRecognizer()
        {
            speechRecognizer = new SpeechRecognizer();

            // Set event handlers
            speechRecognizer.StateChanged += RecognizerStateChanged;
            speechRecognizer.ContinuousRecognitionSession.ResultGenerated += RecognizerResultGenerated;

            // Load Grammer file constraint
            string fileName = String.Format(SRGS_FILE);
            StorageFile grammarContentFile = await Package.Current.InstalledLocation.GetFileAsync(fileName);

            SpeechRecognitionGrammarFileConstraint grammarConstraint = new SpeechRecognitionGrammarFileConstraint(grammarContentFile);

            // Add to grammer constraint
            speechRecognizer.Constraints.Add(grammarConstraint);

            // Compile grammer
            SpeechRecognitionCompilationResult compilationResult = await speechRecognizer.CompileConstraintsAsync();

            Debug.WriteLine("Status: " + compilationResult.Status.ToString());

            // If successful, display the recognition result.
            if (compilationResult.Status == SpeechRecognitionResultStatus.Success)
            {
                Debug.WriteLine("Result: " + compilationResult.ToString());

                await speechRecognizer.ContinuousRecognitionSession.StartAsync();
            }
            else
            {
                Debug.WriteLine("Status: " + compilationResult.Status);
            }

        }

        private void RecognizerStateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {
            Debug.WriteLine("Speech recognizer state: " + args.State.ToString());

        }

        private void RecognizerResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            Debug.WriteLine(args.Result.Status);
            Debug.WriteLine(args.Result.Text);

            sendToAzure(args.Result.Text);


            int count = args.Result.SemanticInterpretation.Properties.Count;

            Debug.WriteLine("Count: " + count);
            Debug.WriteLine("Tag: " + args.Result.Constraint.Tag);

            // Check for different tags and initialize the variables
            String target = args.Result.SemanticInterpretation.Properties.ContainsKey(TAG_TARGET) ?
                            args.Result.SemanticInterpretation.Properties[TAG_TARGET][0].ToString() :
                            "";

            String cmd = args.Result.SemanticInterpretation.Properties.ContainsKey(TAG_CMD) ?
                            args.Result.SemanticInterpretation.Properties[TAG_CMD][0].ToString() :
                            "";

            String device = args.Result.SemanticInterpretation.Properties.ContainsKey(TAG_DEVICE) ?
                            args.Result.SemanticInterpretation.Properties[TAG_DEVICE][0].ToString() :
                            "";

            Debug.WriteLine("Target: " + target + ", Command: " + cmd + ", Device: " + device);

        }


        private void sendBtn_Click(object sender, RoutedEventArgs e)
        {
            sendToAzure(inputTB.Text.ToLower());
            //await speechRecognizer.StopRecognitionAsync();
            //speechRecognizer.Dispose();
        }

        private void sendToAzure(string text)
        {
            IoTHubConnector.SendDeviceToCloudMessagesAsync(text);
            //speechRecognizer.Dispose();
        }

        public static async void readMessage(string text)
        {
            Debug.WriteLine(text);
            SpeechSynthesisStream stream = await synth.SynthesizeTextToStreamAsync(text.ToString());

            mediaElement.SetSource(stream, stream.ContentType);
            state = MediaState.Playing;
            mediaElement.Play();

        }



        private void newExpression(object sender, object e)
        {
            PauseForNewExpression.Stop();
            BlinkTimer.Start();
        }

        private void startBlink(object sender, object e)
        {
            var task = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                timer.Start();
            });
        }

        private void TimerCallback(object sender, object e)
        {
            blink = !blink;
            if (blink)
            {
                var task = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    stateNormal();
                    timer.Stop();
                });
            }
            else
            {
                var task = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    stateClosedEyes();
                });
            }
        }

        private void stateNormal()
        {
            L1.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L2.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L3.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L4.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L5.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L6.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L7.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L8.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            L9.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L10.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L11.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L12.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L13.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L14.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L15.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L16.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            L17.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L18.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L19.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L20.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L21.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L22.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L23.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L24.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            L25.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L26.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L27.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L28.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L29.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L30.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L31.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L32.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            L33.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L34.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L35.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L36.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L37.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L38.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L39.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L40.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            L41.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L42.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L43.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L44.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L45.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L46.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L47.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L48.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            L49.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L50.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L51.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L52.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L53.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L54.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L55.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L56.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            R1.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R2.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R3.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R4.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R5.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R6.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R7.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R8.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            R9.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R10.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R11.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R12.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R13.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R14.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R15.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R16.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            R17.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R18.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R19.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R20.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R21.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R22.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R23.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R24.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            R25.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R26.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R27.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R28.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R29.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R30.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R31.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R32.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            R33.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R34.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R35.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R36.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R37.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R38.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R39.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R40.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            R41.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R42.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R43.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R44.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R45.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R46.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R47.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R48.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            R49.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R50.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R51.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R52.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R53.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R54.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R55.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R56.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        }

        private void stateSurprise()
        {
            L1.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L2.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L3.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L4.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L5.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L6.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L7.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L8.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            L9.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L10.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L11.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L12.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L13.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L14.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L15.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L16.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            L17.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L18.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L19.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L20.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L21.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L22.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L23.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L24.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));

            L25.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L26.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L27.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L28.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L29.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L30.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L31.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L32.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));

            L33.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L34.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L35.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L36.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L37.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L38.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L39.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L40.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));

            L41.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L42.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L43.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L44.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L45.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L46.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L47.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L48.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            L49.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L50.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L51.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L52.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L53.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L54.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L55.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L56.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            R1.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R2.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R3.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R4.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R5.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R6.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R7.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R8.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            R9.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R10.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R11.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R12.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R13.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R14.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R15.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R16.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            R17.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R18.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R19.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R20.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R21.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R22.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R23.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R24.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));

            R25.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R26.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R27.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R28.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R29.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R30.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R31.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R32.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));

            R33.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R34.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R35.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R36.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R37.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R38.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R39.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R40.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));

            R41.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R42.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R43.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R44.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R45.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R46.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R47.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R48.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            R49.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R50.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R51.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R52.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R53.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R54.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R55.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R56.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        }

        private void stateClosedEyes()
        {
            L1.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L2.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L3.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L4.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L5.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L6.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L7.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L8.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            L9.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L10.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L11.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L12.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L13.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L14.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L15.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L16.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            L17.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L18.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L19.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L20.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L21.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L22.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L23.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L24.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            L25.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L26.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L27.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L28.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L29.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L30.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L31.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L32.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            L33.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L34.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L35.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L36.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L37.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L38.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L39.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L40.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            L41.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L42.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L43.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L44.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L45.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L46.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L47.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L48.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            L49.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L50.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L51.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L52.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L53.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            L54.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L55.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            L56.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            R1.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R2.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R3.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R4.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R5.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R6.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R7.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R8.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            R9.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R10.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R11.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R12.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R13.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R14.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R15.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R16.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            R17.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R18.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R19.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R20.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R21.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R22.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R23.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R24.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            R25.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R26.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R27.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R28.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R29.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R30.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R31.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R32.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            R33.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R34.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R35.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R36.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R37.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R38.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R39.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R40.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            R41.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R42.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R43.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R44.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R45.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R46.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R47.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R48.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            R49.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R50.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R51.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R52.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R53.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            R54.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R55.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            R56.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        }

        public enum MediaState
        {
            Stopped,
            Playing,
            Paused
        }


    }
}
