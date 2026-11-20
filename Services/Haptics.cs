namespace PooSweep.Services;

/// <summary>Vibration feedback, respecting the user's setting and failing silently where unsupported.</summary>
public sealed class Haptics(Settings settings)
{
    public void Tap() => Perform(HapticFeedbackType.Click);

    public void Press() => Perform(HapticFeedbackType.LongPress);

    public void Thud()
    {
        if (!settings.Haptics) return;
        try
        {
            if (Vibration.Default.IsSupported)
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(260));
        }
        catch
        {
            // not available on this device
        }
    }

    private void Perform(HapticFeedbackType type)
    {
        if (!settings.Haptics) return;
        try
        {
            if (HapticFeedback.Default.IsSupported)
                HapticFeedback.Default.Perform(type);
        }
        catch
        {
            // not available on this device
        }
    }
}
