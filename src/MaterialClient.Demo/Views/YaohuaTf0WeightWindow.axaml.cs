using System.Collections.ObjectModel;
using System.IO.Ports;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MaterialClient.Demo.Models;

namespace MaterialClient.Demo.Views;

public partial class YaohuaTf0WeightWindow : Window
{
    private const int BaudRate = 9600;
    private const int DataBits = 8;
    private const string EmptyValue = "-";
    private const string UncapturedTare = "0";

    private readonly object _pendingLock = new();
    private readonly List<byte> _pending = [];

    private IReadOnlyList<YaohuaTf0Frame> _frames = [];
    private int _index;
    private YaohuaTf0Frame? _capturedGross;
    private YaohuaTf0Frame? _capturedTare;
    private readonly Queue<string> _recentHex = new();
    private readonly Dictionary<string, FrameKindRow> _kinds = new(StringComparer.Ordinal);
    private readonly ObservableCollection<FrameKindRow> _kindRows = [];
    private YaohuaTf0Frame? _serialLive;
    private string? _beforeTareHex;
    private string? _afterTareHex;
    private SerialPort? _port;
    private CancellationTokenSource? _readCts;
    private long _lastHexUiTicks;
    private long _lastKindUiTicks;

    public YaohuaTf0WeightWindow()
    {
        InitializeComponent();
        HexInput.Text =
            """
            02 2D 30 30 30 31 33 30 32 31 44 03
            02 2B 30 30 30 32 30 35 32 31 45 03
            """;
        ReloadPorts();
        Closed += (_, _) => ClosePort();
    }

    private bool SerialOwnsLive => _port is { IsOpen: true } && _serialLive is not null;

    private YaohuaTf0Frame? Live =>
        SerialOwnsLive ? _serialLive : _frames.Count == 0 ? null : _frames[_index];

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
            ReadTimeout = 200
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
        _serialLive = null;
        _recentHex.Clear();
        HexInput.Text = string.Empty;
        _readCts = new CancellationTokenSource();
        var token = _readCts.Token;
        Task.Run(() => ReadLoop(port, token), token);
        StatusText.Text = $"{portName} open {BaudRate} 8N1";
        Refresh(null);
    }

    private void OnDisconnectClick(object? sender, RoutedEventArgs e)
    {
        ClosePort();
        StatusText.Text = "closed";
        Refresh(null);
    }

    private void OnLoadClick(object? sender, RoutedEventArgs e)
    {
        var scan = YaohuaTf0HexScan.FromHex(HexInput.Text);
        var status = $"accepted {scan.Frames.Count}, discarded {scan.DiscardedCount}";
        if (scan.Frames.Count == 0)
        {
            StatusText.Text = status;
            return;
        }

        _frames = scan.Frames;
        _index = 0;
        Refresh(status);
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e)
    {
        if (SerialOwnsLive || _index <= 0) return;
        _index--;
        Refresh(null);
    }

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        if (SerialOwnsLive || _index >= _frames.Count - 1) return;
        _index++;
        Refresh(null);
    }

    private void OnCaptureGrossClick(object? sender, RoutedEventArgs e)
    {
        if (Live is null) return;
        _capturedGross = Live;
        Refresh(null);
    }

    private void OnCaptureTareClick(object? sender, RoutedEventArgs e)
    {
        if (Live is null) return;
        _capturedTare = Live;
        Refresh(null);
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        _capturedGross = null;
        _capturedTare = null;
        Refresh(null);
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

    private void ReadLoop(SerialPort port, CancellationToken token)
    {
        var buffer = new byte[256];
        while (!token.IsCancellationRequested)
        {
            int read;
            try
            {
                read = port.Read(buffer, 0, buffer.Length);
            }
            catch (TimeoutException)
            {
                continue;
            }
            catch (Exception)
            {
                break;
            }

            if (read <= 0) continue;

            FrameScan scan;
            lock (_pendingLock)
            {
                for (var i = 0; i < read; i++)
                    _pending.Add(buffer[i]);
                scan = YaohuaFrameScanner.Extract(_pending);
            }

            if (scan.LatestWeight is null && scan.Frames.Count == 0) continue;
            var batch = scan;
            Dispatcher.UIThread.Post(() => ApplySerialBatch(batch));
        }
    }

    private void ApplySerialBatch(FrameScan scan)
    {
        if (_port is not { IsOpen: true }) return;

        foreach (var frame in scan.Frames)
            ObserveKind(frame);

        if (scan.LatestWeight is null) return;
        _serialLive = scan.LatestWeight;
        _recentHex.Enqueue(scan.LatestWeight.RawHex);
        while (_recentHex.Count > 8)
            _recentHex.Dequeue();

        var now = Environment.TickCount64;
        if (now - _lastHexUiTicks >= 200)
        {
            _lastHexUiTicks = now;
            HexInput.Text = string.Join(Environment.NewLine, _recentHex);
            HexInput.CaretIndex = HexInput.Text?.Length ?? 0;
        }

        Refresh(null);
        if (_beforeTareHex is null || _afterTareHex is null)
            TareCheckLog.Text = scan.LatestWeight.DescribeChecksum();
    }

    private void OnSampleBeforeTareClick(object? sender, RoutedEventArgs e) =>
        StoreTareSample(before: true);

    private void OnSampleAfterTareClick(object? sender, RoutedEventArgs e) =>
        StoreTareSample(before: false);

    private void StoreTareSample(bool before)
    {
        if (Live is null)
        {
            TareCheckLog.Text = "no frame";
            return;
        }

        if (before)
            _beforeTareHex = Live.RawHex;
        else
            _afterTareHex = Live.RawHex;

        if (_beforeTareHex is null || _afterTareHex is null)
        {
            TareCheckLog.Text = before
                ? $"before stored{Environment.NewLine}{Live.DescribeChecksum()}{Environment.NewLine}{Live.RawHex}"
                : $"after stored{Environment.NewLine}{Live.DescribeChecksum()}{Environment.NewLine}{Live.RawHex}";
            return;
        }

        TareCheckLog.Text = YaohuaTf0Frame.CompareSamples(_beforeTareHex, _afterTareHex);
    }

    private void ObserveKind(ObservedFrame frame)
    {
        var signature = YaohuaFrameScanner.Signature(frame.Bytes);
        var isNew = false;
        if (_kinds.TryGetValue(signature, out var row))
        {
            row.Count++;
        }
        else
        {
            isNew = true;
            row = new FrameKindRow(
                signature,
                YaohuaTf0Frame.FormatHex(frame.Bytes),
                frame.Bytes.Length,
                frame.Weight is null ? "other" : "weight");
            _kinds[signature] = row;
            _kindRows.Add(row);
        }

        RefreshKindLog(isNew);
    }

    private void RefreshKindLog(bool force)
    {
        var now = Environment.TickCount64;
        if (!force && now - _lastKindUiTicks < 200) return;
        _lastKindUiTicks = now;

        if (_kindRows.Count == 0)
        {
            KindLog.Text = "(none)";
            return;
        }

        var lines = new List<string>(_kindRows.Count);
        foreach (var row in _kindRows)
            lines.Add($"{row.Count}  len={row.Length}  {row.Kind}{Environment.NewLine}{row.ExampleHex}");

        KindLog.Text = string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    private void OnClearKindsClick(object? sender, RoutedEventArgs e)
    {
        _kinds.Clear();
        _kindRows.Clear();
        KindLog.Text = "(none)";
    }

    private void ClosePort()
    {
        var cts = _readCts;
        _readCts = null;
        cts?.Cancel();

        var port = _port;
        _port = null;
        _serialLive = null;
        _recentHex.Clear();
        lock (_pendingLock)
            _pending.Clear();

        if (port is not null)
        {
            try
            {
                if (port.IsOpen) port.Close();
            }
            catch (Exception)
            {
                // Port may already be closed by the read loop.
            }

            port.Dispose();
        }

        cts?.Dispose();
    }

    private void Refresh(string? loadStatus)
    {
        var live = Live;
        LiveValueText.Text = live is null ? EmptyValue : live.FormatDisplayedWeight();

        var gross = _capturedGross ?? live;
        GrossValueText.Text = gross is null ? EmptyValue : gross.FormatDisplayedWeight();
        TareValueText.Text = _capturedTare is null ? UncapturedTare : _capturedTare.FormatDisplayedWeight();

        if (gross is null)
            NetValueText.Text = EmptyValue;
        else if (_capturedTare is null)
            NetValueText.Text = gross.FormatDisplayedWeight();
        else
            NetValueText.Text = YaohuaTf0Frame.FormatDifference(gross, _capturedTare);

        FrameIndexText.Text = SerialOwnsLive
            ? "serial"
            : _frames.Count == 0
                ? "frame 0/0"
                : $"frame {_index + 1}/{_frames.Count}";

        if (loadStatus is not null)
            StatusText.Text = loadStatus;
    }
}
