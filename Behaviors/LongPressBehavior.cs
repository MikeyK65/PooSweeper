using System.Windows.Input;

namespace PooSweep.Behaviors;

public sealed class LongPressBehavior : Behavior<View>
{
    private CancellationTokenSource? _cts;
    private PointerGestureRecognizer? _pointer;
    private WeakReference<View>? _attachedView;
    private volatile bool _isAttached;

    private EventHandler<PointerEventArgs>? _pressed;
    private EventHandler<PointerEventArgs>? _released;
    private EventHandler<PointerEventArgs>? _exited;

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command),
        typeof(ICommand),
        typeof(LongPressBehavior));

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter),
        typeof(object),
        typeof(LongPressBehavior));

    public static readonly BindableProperty DurationProperty = BindableProperty.Create(
        nameof(Duration),
        typeof(int),
        typeof(LongPressBehavior),
        450);

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public int Duration
    {
        get => (int)GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    protected override void OnAttachedTo(View bindable)
    {
        base.OnAttachedTo(bindable);

        _isAttached = true;
        _attachedView = new WeakReference<View>(bindable);

        _pointer = new PointerGestureRecognizer();
        _pressed = (_, _) => SafeStart();
        _released = (_, _) => SafeCancel();
        _exited = (_, _) => SafeCancel();

        _pointer.PointerPressed += _pressed;
        _pointer.PointerReleased += _released;
        _pointer.PointerExited += _exited;

        bindable.GestureRecognizers.Add(_pointer);
    }

    protected override void OnDetachingFrom(View bindable)
    {
        _isAttached = false;
        _attachedView = null;

        SafeCancel();

        if (_pointer is not null)
        {
            if (_pressed is not null) _pointer.PointerPressed -= _pressed;
            if (_released is not null) _pointer.PointerReleased -= _released;
            if (_exited is not null) _pointer.PointerExited -= _exited;

            bindable.GestureRecognizers.Remove(_pointer);
        }

        _pointer = null;
        _pressed = null;
        _released = null;
        _exited = null;

        base.OnDetachingFrom(bindable);
    }

    private bool CanRun()
    {
        if (!_isAttached) return false;
        if (_attachedView is null) return false;
        return _attachedView.TryGetTarget(out _);
    }

    private void SafeStart()
    {
        try
        {
            if (!CanRun()) return;
            Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LongPressBehavior] Exception in PointerPressed handler: {ex}");
        }
    }

    private void SafeCancel()
    {
        try
        {
            Cancel();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LongPressBehavior] Exception in PointerReleased/Exited handler: {ex}");
        }
    }

    private void Start()
    {
        Cancel();
        _cts = new CancellationTokenSource();
        _ = FireAsync(_cts.Token);
    }

    private void Cancel()
    {
        try { _cts?.Cancel(); }
        catch { }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task FireAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(Duration, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested) return;
        if (!CanRun()) return;

        var cmd = Command;
        if (cmd is null) return;

        var param = CommandParameter;

        try
        {
            System.Diagnostics.Debug.WriteLine($"[LongPressBehavior] About to execute command. Param type: {param?.GetType().FullName}, Value: {param}");

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!CanRun()) return;

                try
                {
                    if (cmd.CanExecute(param))
                        cmd.Execute(param);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LongPressBehavior] Exception in command execution (UI thread): {ex}\nStackTrace: {ex.StackTrace}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LongPressBehavior] Exception scheduling command execution: {ex}\nStackTrace: {ex.StackTrace}");
        }
    }
}
