using System.IO.Ports;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MaterialClient.Demo.Models;

namespace MaterialClient.Demo.Views;

public partial class YaohuaTf1WeightWindow : Window
{
    private const int BaudRate = 9600;
    private const int DataBits = 8;
    private const int ReplyTimeoutMs = 700;
    private const string EmptyValue = "-";

    private readonly SemaphoreSlim _io = new(1, 1);
    private readonly Queue<string> _log = new();

    private SerialPort? _port;
    private CancellationTokenSource? _pollCts;

    public YaohuaTf1WeightWindow()
    {
        InitializeComponent();
        AddressList.ItemsSource = Enumerable.Range(0, 26).Select(i => ((char)('A' + i)).ToString()).ToArray();
        AddressList.SelectedIndex = 0;
        ReloadPorts();
        Closed += (_, _) => ClosePort();
    }

    private void OnRefreshPortsClick(object? sender, RoutedEventArgs e) => ReloadPorts();

    private void OnConnectClick(object? sender, RoutedEventArgs e)
    {
        if (PortList.SelectedItem is not string portName || string.IsNullOrWhiteSpace(portName))
        {
            StatusText.Text = "no port selected";
            return;
        }

        ClosePort();
        var port = new SerialPort(portName, BaudRate, Parity.None, DataBits, StopBits.One)
        {
            Handshake = Handshake.None,
            ReadTimeout = 100,
            WriteTimeout = 500
        };

        try
        {
            port.Open();
        }
        catch (Exception ex)
        {
            port.Dispose();
            StatusText.Text = ex.Message;
            return;
        }

        _port = port;
        StatusText.Text = $"{portName} open {BaudRate} 8N1 tf1";
    }

    private void OnDisconnectClick(object? sender, RoutedEventArgs e)
    {
        ClosePort();
        StatusText.Text = "closed";
    }

    private void OnHandshakeClick(object? sender, RoutedEventArgs e) => _ = QueryAsync('A');

    private void OnReadGrossClick(object? sender, RoutedEventArgs e) => _ = QueryAsync('B');

    private void OnReadTareClick(object? sender, RoutedEventArgs e) => _ = QueryAsync('C');

    private void OnReadNetClick(object? sender, RoutedEventArgs e) => _ = QueryAsync('D');

    private void OnReadVehicleNoClick(object? sender, RoutedEventArgs e) => _ = QueryAsync('E');

    private void OnReadGoodsNoClick(object? sender, RoutedEventArgs e) => _ = QueryAsync('F');

    private void OnPollClick(object? sender, RoutedEventArgs e)
    {
        if (_port is not { IsOpen: true } || _pollCts is not null) return;
        _pollCts = new CancellationTokenSource();
        var token = _pollCts.Token;
        _ = Task.Run(() => PollLoop(token), token);
        StatusText.Text = "polling B C D";
    }

    private void OnStopPollClick(object? sender, RoutedEventArgs e) => StopPoll();

    private async Task PollLoop(CancellationToken token)
    {
        var commands = new[] { 'B', 'C', 'D' };
        var index = 0;
        while (!token.IsCancellationRequested)
        {
            await QueryAsync(commands[index % commands.Length], token);
            index++;
            try
            {
                await Task.Delay(200, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task QueryAsync(char command, CancellationToken token = default)
    {
        var port = _port;
        if (port is not { IsOpen: true })
        {
            Dispatcher.UIThread.Post(() => StatusText.Text = "not open");
            return;
        }

        if (AddressList.SelectedItem is not string addressText || addressText.Length != 1)
        {
            Dispatcher.UIThread.Post(() => StatusText.Text = "no address");
            return;
        }

        var request = YaohuaTf1Protocol.Build(addressText[0], command);
        await _io.WaitAsync(token);
        try
        {
            byte[] raw;
            try
            {
                raw = await Task.Run(() => Exchange(port, request.Bytes), token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Dispatcher.UIThread.Post(() => StatusText.Text = ex.Message);
                return;
            }

            var reply = YaohuaTf1Protocol.TryParse(raw);
            Dispatcher.UIThread.Post(() => ApplyReply(request, raw, reply));
        }
        finally
        {
            _io.Release();
        }
    }

    private static byte[] Exchange(SerialPort port, byte[] request)
    {
        port.DiscardInBuffer();
        port.Write(request, 0, request.Length);

        var received = new List<byte>();
        var deadline = Environment.TickCount64 + ReplyTimeoutMs;
        var sawStart = false;
        while (Environment.TickCount64 < deadline && received.Count < 32)
        {
            int value;
            try
            {
                value = port.ReadByte();
            }
            catch (TimeoutException)
            {
                continue;
            }

            if (value < 0) continue;
            var b = (byte)value;
            if (!sawStart)
            {
                if (b != 0x02) continue;
                sawStart = true;
            }

            received.Add(b);
            if (b == 0x03) break;
        }

        return received.ToArray();
    }

    private void ApplyReply(YaohuaTf1Command request, byte[] raw, YaohuaTf1Reply? reply)
    {
        var rx = raw.Length == 0 ? "timeout" : YaohuaTf0Frame.FormatHex(raw);
        AppendLog($"tx {request.Command} {request.RawHex}{Environment.NewLine}rx {rx}");

        if (reply is null)
        {
            StatusText.Text = raw.Length == 0 ? "timeout" : $"unparsed len={raw.Length}";
            return;
        }

        StatusText.Text =
            $"cmd {reply.Command} addr {reply.Address} {(reply.ChecksumMatch ? "xor match" : "xor MISMATCH")}";

        switch (reply.Command)
        {
            case 'B' when reply.Weight is not null:
                GrossValueText.Text = reply.Weight.FormatDisplayedWeight();
                break;
            case 'C' when reply.Weight is not null:
                TareValueText.Text = reply.Weight.FormatDisplayedWeight();
                break;
            case 'D' when reply.Weight is not null:
                NetValueText.Text = reply.Weight.FormatDisplayedWeight();
                break;
            case 'E' when !string.IsNullOrEmpty(reply.TextPayload):
                VehicleNoText.Text = reply.TextPayload;
                break;
            case 'F' when !string.IsNullOrEmpty(reply.TextPayload):
                GoodsNoText.Text = reply.TextPayload;
                break;
        }
    }

    private void AppendLog(string line)
    {
        _log.Enqueue(line);
        while (_log.Count > 6)
            _log.Dequeue();
        HexLog.Text = string.Join(Environment.NewLine + Environment.NewLine, _log);
        HexLog.CaretIndex = HexLog.Text?.Length ?? 0;
    }

    private void ReloadPorts()
    {
        var selected = PortList.SelectedItem as string;
        var names = SerialPort.GetPortNames().OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        PortList.ItemsSource = names;
        if (selected is not null && names.Contains(selected, StringComparer.OrdinalIgnoreCase))
            PortList.SelectedItem = names.First(name => string.Equals(name, selected, StringComparison.OrdinalIgnoreCase));
        else if (names.Length > 0)
            PortList.SelectedItem = names[0];
    }

    private void StopPoll()
    {
        var cts = _pollCts;
        _pollCts = null;
        cts?.Cancel();
        cts?.Dispose();
    }

    private void ClosePort()
    {
        StopPoll();
        var port = _port;
        _port = null;
        if (port is null) return;
        try
        {
            if (port.IsOpen) port.Close();
        }
        catch (Exception)
        {
            // Port may already be closed.
        }

        port.Dispose();
    }
}
