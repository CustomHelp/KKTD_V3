using System;
using System.Text;
using System.Windows.Input;

namespace KKTD_V3.Services;

/// <summary>
/// USB-HID-Barcode-Scanner laufen typischerweise als Tastatur-Emulation.
/// Diese Service sammelt Tasten-Events bis zum Enter und feuert ein Event.
/// </summary>
public sealed class BarcodeService
{
    private readonly StringBuilder _buffer = new();
    private DateTime _lastKey = DateTime.MinValue;

    /// <summary>Maximaler Abstand zwischen zwei Zeichen, ab dem der Buffer verworfen wird.</summary>
    public TimeSpan MaxKeyGap { get; set; } = TimeSpan.FromMilliseconds(80);

    public event EventHandler<string>? BarcodeScanned;

    /// <summary>Aus einem globalen WPF-PreviewKeyDown-Handler aufrufen.</summary>
    public bool HandlePreviewKey(KeyEventArgs e)
    {
        var now = DateTime.UtcNow;
        if (now - _lastKey > MaxKeyGap) _buffer.Clear();
        _lastKey = now;

        if (e.Key == Key.Enter)
        {
            if (_buffer.Length > 0)
            {
                var code = _buffer.ToString();
                _buffer.Clear();
                BarcodeScanned?.Invoke(this, code);
                return true;
            }
            return false;
        }

        char? c = KeyToChar(e.Key);
        if (c.HasValue)
        {
            _buffer.Append(c.Value);
            return true;
        }

        return false;
    }

    private static char? KeyToChar(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => (char)('0' + (key - Key.D0)),
        >= Key.NumPad0 and <= Key.NumPad9 => (char)('0' + (key - Key.NumPad0)),
        >= Key.A and <= Key.Z => (char)('A' + (key - Key.A)),
        Key.OemMinus or Key.Subtract => '-',
        Key.OemPeriod or Key.Decimal => '.',
        _ => null
    };
}
