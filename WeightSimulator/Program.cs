using System.IO.Ports;

const float DECIMAL_STEP = 0.1f;
Random random = new Random();

// SERIAL

Console.WriteLine("Select the Serial port you want to connect:");

var ports = SerialPort.GetPortNames();
if (ports.Length == 0)
{
    Console.WriteLine("No serial ports available.");
    return;
}

int i = 0;
Array.ForEach(ports, x => Console.WriteLine($"{i++}: {x}"));

if (!int.TryParse(Console.ReadLine(), out int selectedPortIndex) || selectedPortIndex < 0 || selectedPortIndex >= ports.Length)
{
    Console.WriteLine("Invalid port selection.");
    return;
}

var selectedPort = ports[selectedPortIndex];

// BAUD RATE

Console.WriteLine("\nSelect the baud rate for the connection:");
int[] baudRates = { 4800, 9600, 19200, 38400, 57600, 115200 };

i = 0;
Array.ForEach(baudRates, x => Console.WriteLine($"{i++}: {x}"));

if (!int.TryParse(Console.ReadLine(), out int selectedBaudRateIndex) || selectedBaudRateIndex < 0 || selectedBaudRateIndex >= baudRates.Length)
{
    Console.WriteLine("Invalid baud rate selection.");
    return;
}

var selectedbaudRate = baudRates[selectedBaudRateIndex];
using SerialPort serialPort = new SerialPort(selectedPort)
{
    BaudRate = selectedbaudRate,
    DataBits = 7,
    Parity = Parity.Even,
    StopBits = StopBits.Two,
    Handshake = Handshake.None,
};

try
{
    serialPort.Open();
    Console.WriteLine($"Connected to {selectedPort}. Sending data... Press 'Ctrl+C' to stop.");
    serialPort.WriteLine("000000");
    float currentWeight = 0.0f;

    string message = $"PB: {currentWeight:0000.0}kg PL: {currentWeight:0000.0}kg T:0.0kg";
    string oldMessage = string.Empty;
    string sobre = "SOBRE";
    bool isSobre = false;

    Queue<float> weightQueue = new Queue<float>();

    CancellationTokenSource cts = new CancellationTokenSource();

    // Start sending data in a separate thread
    var sendDataThread = new Thread(() =>
    {
        while (!cts.Token.IsCancellationRequested)
        {
            if (weightQueue.Count > 0)
            {
                //$"PB: XXXXXXkg PL: YYYYYYkg T:ZZZZZZkg"
                currentWeight = weightQueue.Dequeue();
                message = $"PB: {currentWeight:0000.0}kg PL: {currentWeight:0000.0}kg T:1.0kg";
            }
            else if (isSobre)
            {
                message = sobre;
            }
            else
            {
                message = $"PB: {currentWeight:0000.0}kg PL: {currentWeight:0000.0}kg T:1.0kg";
            }
            message = message.Replace(".", ",");

            serialPort.Write(message + "\r\n");

            if (message != oldMessage)
            {
                Console.WriteLine(message);
                oldMessage = message;
            }
            Thread.Sleep(30);
        }
    });
    
    sendDataThread.Start();

    // Wait for Ctrl+C or any other termination signal
    Console.CancelKeyPress += (sender, eventArgs) =>
    {
        eventArgs.Cancel = true; // Prevent the process from terminating immediately.
        cts.Cancel();
        sendDataThread.Join();
        Console.WriteLine("\nTransmission stopped.");
    };

    // Keep the main thread alive to keep sending data
    while (!cts.Token.IsCancellationRequested)
    {
        var sucess = float.TryParse(Console.ReadLine(), out var weightGoal);
        if (sucess)
        {
            var steps = CalcStepsForNextWeightGoal(weightGoal, currentWeight);
            Array.ForEach(steps, weightQueue.Enqueue);
            isSobre = false;
        }
        else
        {
            isSobre = true;
        }

        Console.Write("\nNext weight goal: ");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
finally
{
    serialPort.Close();
}

float[] CalcStepsForNextWeightGoal(float weightGoal, float currentWeight)
{
    bool shouldRevert = false;
    if (weightGoal < currentWeight)
    {
        var temp = weightGoal;
        weightGoal = currentWeight;
        currentWeight = temp;

        shouldRevert = true;
    }

    var diff = weightGoal - currentWeight;

    int steps = Math.Abs(Convert.ToInt32((int)double.Ceiling(diff / DECIMAL_STEP)));
    int fractionedSteps = random.Next((int)double.Ceiling(steps / 10), steps);
    fractionedSteps = Math.Min(fractionedSteps, 25);
    if (fractionedSteps <= 0) fractionedSteps = 1;

    int stepsToIncrementPerIteration = steps / fractionedSteps;

    float increment = stepsToIncrementPerIteration * DECIMAL_STEP;

    float incrementalWeight = currentWeight;
    List<float> weightSteps = [];

    var index = DECIMAL_STEP.ToString().IndexOf(".");
    string decimalString = DECIMAL_STEP.ToString();

    int digits = index == -1 ? 0 : decimalString.Remove(0, index).Length;

    weightGoal = float.Round(weightGoal, digits);

    while (incrementalWeight < weightGoal)
    {
        incrementalWeight += float.Round(increment, digits);

        if (incrementalWeight + increment > weightGoal)
            incrementalWeight = weightGoal;

        weightSteps.Add(float.Round(incrementalWeight, digits));
    }

    if (shouldRevert)
    {
        weightSteps.Reverse();

        weightSteps.Add(currentWeight);
    }


    return weightSteps.ToArray();
}