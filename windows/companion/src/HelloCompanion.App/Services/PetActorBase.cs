using HelloCompanion.App.Models;

namespace HelloCompanion.App.Services;

internal abstract class PetActorBase : IPetActor
{
    private readonly PetDefinition _definition;
    private readonly PetMovementArea _movementArea;
    private readonly Dictionary<string, AnimationClip> _animations;
    private readonly NativeSpriteWindow _window;
    private readonly PetSpeechBubble _speechBubble;
    private readonly DesktopBounds _virtualScreen;
    private readonly TaskbarPlacement _taskbar;
    private readonly SemaphoreSlim _behaviorGate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly Random _random;
    private double _x;
    private double _y;
    private double _velocityX;
    private double _velocityY;
    private double _animationTime;
    private string _currentAnimation = "roam";
    private bool _movementPaused;
    private bool _ambientBehaviorActive;
    private double _ambientCountdownSeconds;
    private double _ambientDurationSeconds;
    private bool _disposed;

    protected PetActorBase(
        PetDefinition definition,
        PetMovementArea movementArea,
        int index,
        int totalCount)
    {
        _definition = definition;
        _movementArea = movementArea;
        _virtualScreen = DesktopGeometry.GetVirtualScreen();
        _taskbar = DesktopGeometry.GetTaskbarPlacement(_virtualScreen);
        _animations = LoadAnimations(definition);
        _random = new Random(HashCode.Combine(definition.Id, index, Environment.TickCount));
        ResetAmbientCountdown();
        bool acceptsClicks = definition.ClickBehavior.Length > 0;
        _window = new NativeSpriteWindow(acceptsClicks);
        if (acceptsClicks)
        {
            _window.Clicked += Window_Clicked;
        }
        _speechBubble = new PetSpeechBubble();

        double fraction = (index + 1d) / (totalCount + 1d);
        _velocityX = definition.SpeedPixelsPerSecond * (index % 2 == 0 ? 1 : -1);
        _velocityY = definition.SpeedPixelsPerSecond * 0.62 * (index % 2 == 0 ? 1 : -1);

        if (movementArea == PetMovementArea.FullScreen)
        {
            _x = _virtualScreen.Left + ((_virtualScreen.Width - definition.SpriteWidth) * fraction);
            _y = _virtualScreen.Top + ((_virtualScreen.Height - definition.SpriteHeight) * (1 - fraction));
        }
        else
        {
            InitializeTaskbarPosition(fraction);
        }

        Render();
    }

    public string Id => _definition.Id;
    public string DisplayName => _definition.DisplayName;
    public bool IsBusy { get; private set; }

    protected PetDefinition Definition => _definition;

    private async void Window_Clicked(object? sender, EventArgs e)
    {
        if (_disposed || IsBusy)
        {
            return;
        }

        try
        {
            await HandleClickAsync(_lifetimeCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // The pet was removed while its click reaction was running.
        }
    }

    public void Update(double elapsedSeconds)
    {
        if (_disposed)
        {
            return;
        }

        if (!IsBusy)
        {
            UpdateAmbientBehavior(elapsedSeconds);
        }

        _animationTime += elapsedSeconds;

        if (!_movementPaused)
        {
            if (_movementArea == PetMovementArea.FullScreen)
            {
                UpdateFullScreen(elapsedSeconds);
            }
            else
            {
                UpdateTaskbar(elapsedSeconds);
            }
        }

        Render();
    }

    public abstract Task HandleReminderAsync(
        ReminderContext reminder,
        CancellationToken cancellationToken = default);

    public virtual Task HandleClickAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public void Pause() => _movementPaused = true;

    public void Resume()
    {
        _ambientBehaviorActive = false;
        _movementPaused = false;
        PlayAnimation("roam");
        ResetAmbientCountdown();
    }

    protected async Task RunExclusiveBehaviorAsync(
        Func<CancellationToken, Task> behavior,
        CancellationToken cancellationToken)
    {
        await _behaviorGate.WaitAsync(cancellationToken);
        IsBusy = true;

        try
        {
            await behavior(cancellationToken);
        }
        finally
        {
            _speechBubble.Hide();
            Resume();
            IsBusy = false;
            _behaviorGate.Release();
        }
    }

    protected Task StopMovingAsync()
    {
        Pause();
        return Task.CompletedTask;
    }

    protected Task PlayAnimationAsync(string animation)
    {
        PlayAnimation(animation);
        return Task.CompletedTask;
    }

    protected Task ShowMessageAsync(ReminderContext reminder)
    {
        _speechBubble.Show(
            reminder.Title,
            reminder.Message,
            new PetScreenBounds(
                (int)Math.Round(_x),
                (int)Math.Round(_y),
                _definition.SpriteWidth,
                _definition.SpriteHeight));
        return Task.CompletedTask;
    }

    protected Task HideMessageAsync()
    {
        _speechBubble.Hide();
        return Task.CompletedTask;
    }

    protected static Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken)
        => Task.Delay(duration, cancellationToken);

    protected Task ResumePreviousBehaviorAsync()
    {
        Resume();
        return Task.CompletedTask;
    }

    private void PlayAnimation(string animation)
    {
        if (!_animations.ContainsKey(animation))
        {
            animation = "roam";
        }

        if (!string.Equals(_currentAnimation, animation, StringComparison.OrdinalIgnoreCase))
        {
            _currentAnimation = animation;
            _animationTime = 0;
        }
    }

    private void UpdateAmbientBehavior(double elapsedSeconds)
    {
        if (_definition.AmbientBehaviors.Length == 0)
        {
            return;
        }

        if (_ambientBehaviorActive)
        {
            _ambientDurationSeconds -= elapsedSeconds;
            if (_ambientDurationSeconds <= 0)
            {
                Resume();
            }

            return;
        }

        _ambientCountdownSeconds -= elapsedSeconds;
        if (_ambientCountdownSeconds > 0)
        {
            return;
        }

        PetAmbientBehavior behavior = ChooseAmbientBehavior();
        _ambientBehaviorActive = true;
        _ambientDurationSeconds = RandomBetween(behavior.MinimumSeconds, behavior.MaximumSeconds);
        _movementPaused = true;
        PlayAnimation(behavior.Animation);
    }

    private PetAmbientBehavior ChooseAmbientBehavior()
    {
        double totalWeight = _definition.AmbientBehaviors.Sum(behavior => behavior.Weight);
        double choice = _random.NextDouble() * totalWeight;
        foreach (PetAmbientBehavior behavior in _definition.AmbientBehaviors)
        {
            choice -= behavior.Weight;
            if (choice <= 0)
            {
                return behavior;
            }
        }

        return _definition.AmbientBehaviors[^1];
    }

    private void ResetAmbientCountdown()
        => _ambientCountdownSeconds = RandomBetween(7, 15);

    private double RandomBetween(double minimum, double maximum)
        => minimum + (_random.NextDouble() * (maximum - minimum));

    private void UpdateFullScreen(double elapsedSeconds)
    {
        _x += _velocityX * elapsedSeconds;
        _y += _velocityY * elapsedSeconds;

        double maximumX = _virtualScreen.Right - _definition.SpriteWidth;
        double maximumY = _virtualScreen.Bottom - _definition.SpriteHeight;

        if (_x <= _virtualScreen.Left || _x >= maximumX)
        {
            _x = Math.Clamp(_x, _virtualScreen.Left, maximumX);
            _velocityX *= -1;
        }

        if (_y <= _virtualScreen.Top || _y >= maximumY)
        {
            _y = Math.Clamp(_y, _virtualScreen.Top, maximumY);
            _velocityY *= -1;
        }
    }

    private void UpdateTaskbar(double elapsedSeconds)
    {
        bool horizontal = _taskbar.Edge is TaskbarEdge.Top or TaskbarEdge.Bottom;
        if (horizontal)
        {
            _x += _velocityX * elapsedSeconds;
            double minimum = _taskbar.Bounds.Left;
            double maximum = _taskbar.Bounds.Right - _definition.SpriteWidth;
            if (_x <= minimum || _x >= maximum)
            {
                _x = Math.Clamp(_x, minimum, maximum);
                _velocityX *= -1;
            }

            double standingY = _taskbar.Edge == TaskbarEdge.Bottom
                ? _taskbar.Bounds.Top - _definition.SpriteHeight
                : _taskbar.Bounds.Bottom;
            _y = standingY - Math.Abs(Math.Sin(_animationTime * 6)) * 3;
        }
        else
        {
            _y += Math.Abs(_velocityX) * Math.Sign(_velocityY) * elapsedSeconds;
            double minimum = _taskbar.Bounds.Top;
            double maximum = _taskbar.Bounds.Bottom - _definition.SpriteHeight;
            if (_y <= minimum || _y >= maximum)
            {
                _y = Math.Clamp(_y, minimum, maximum);
                _velocityY *= -1;
            }

            _x = _taskbar.Edge == TaskbarEdge.Left
                ? _taskbar.Bounds.Right
                : _taskbar.Bounds.Left - _definition.SpriteWidth;
        }
    }

    private void InitializeTaskbarPosition(double fraction)
    {
        bool horizontal = _taskbar.Edge is TaskbarEdge.Top or TaskbarEdge.Bottom;
        if (horizontal)
        {
            _x = _taskbar.Bounds.Left + ((_taskbar.Bounds.Width - _definition.SpriteWidth) * fraction);
            _y = _taskbar.Edge == TaskbarEdge.Bottom
                ? _taskbar.Bounds.Top - _definition.SpriteHeight
                : _taskbar.Bounds.Bottom;
        }
        else
        {
            _y = _taskbar.Bounds.Top + ((_taskbar.Bounds.Height - _definition.SpriteHeight) * fraction);
            _x = _taskbar.Edge == TaskbarEdge.Left
                ? _taskbar.Bounds.Right
                : _taskbar.Bounds.Left - _definition.SpriteWidth;
        }
    }

    private void Render()
    {
        AnimationClip clip = _animations.TryGetValue(_currentAnimation, out AnimationClip? selected)
            ? selected
            : _animations["roam"];
        LayeredSpriteFrame[] frames = _velocityX >= 0 ? clip.RightFrames : clip.LeftFrames;
        int elapsedFrame = (int)(_animationTime * 1000 / clip.FrameDurationMilliseconds);
        int frameIndex = clip.Loops
            ? elapsedFrame % frames.Length
            : Math.Min(elapsedFrame, frames.Length - 1);
        _window.Render(frames[frameIndex], (int)Math.Round(_x), (int)Math.Round(_y));
    }

    private static Dictionary<string, AnimationClip> LoadAnimations(PetDefinition definition)
    {
        Dictionary<string, string[]> sources = new(definition.Animations, StringComparer.OrdinalIgnoreCase);
        if (!sources.ContainsKey("roam"))
        {
            sources["roam"] = definition.Frames;
        }

        Dictionary<string, AnimationClip> clips = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, string[] frames) in sources)
        {
            if (frames.Length == 0)
            {
                continue;
            }

            clips[name] = new AnimationClip(
                LoadFrames(definition, frames, false),
                LoadFrames(definition, frames, true),
                definition.AnimationFrameDurations.TryGetValue(name, out int duration)
                    ? duration
                    : definition.FrameDurationMilliseconds,
                !definition.NonLoopingAnimations.Contains(name, StringComparer.OrdinalIgnoreCase));
        }

        if (!clips.ContainsKey("roam"))
        {
            throw new InvalidDataException($"Pet '{definition.DisplayName}' has no roaming animation frames.");
        }

        return clips;
    }

    private static LayeredSpriteFrame[] LoadFrames(
        PetDefinition definition,
        IEnumerable<string> frameFiles,
        bool flipHorizontally)
        => frameFiles
            .Select(frame => new LayeredSpriteFrame(
                Path.Combine(definition.AssetDirectory, frame),
                definition.SpriteWidth,
                definition.SpriteHeight,
                flipHorizontally,
                definition.PixelArt))
            .ToArray();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetimeCancellation.Cancel();
        _window.Clicked -= Window_Clicked;
        _speechBubble.Dispose();
        _window.Dispose();
        foreach (AnimationClip clip in _animations.Values)
        {
            clip.Dispose();
        }

        _lifetimeCancellation.Dispose();

    }

    private sealed record AnimationClip(
        LayeredSpriteFrame[] RightFrames,
        LayeredSpriteFrame[] LeftFrames,
        int FrameDurationMilliseconds,
        bool Loops) : IDisposable
    {
        public void Dispose()
        {
            foreach (LayeredSpriteFrame frame in RightFrames)
            {
                frame.Dispose();
            }

            foreach (LayeredSpriteFrame frame in LeftFrames)
            {
                frame.Dispose();
            }
        }
    }
}

internal sealed class ConfiguredPetActor : PetActorBase
{
    private static readonly PetBehaviorAction[] DefaultReminderBehavior =
    [
        new PetBehaviorAction { Action = "stop" },
        new PetBehaviorAction { Action = "show-message" },
        new PetBehaviorAction { Action = "wait", Seconds = 5 },
        new PetBehaviorAction { Action = "hide-message" },
        new PetBehaviorAction { Action = "resume" }
    ];

    public ConfiguredPetActor(
        PetDefinition definition,
        PetMovementArea movementArea,
        int index,
        int totalCount)
        : base(definition, movementArea, index, totalCount)
    {
    }

    public override Task HandleReminderAsync(
        ReminderContext reminder,
        CancellationToken cancellationToken = default)
        => RunExclusiveBehaviorAsync(
            token => RunReminderBehaviorAsync(reminder, token),
            cancellationToken);

    public override Task HandleClickAsync(CancellationToken cancellationToken = default)
    {
        if (Definition.ClickBehavior.Length == 0)
        {
            return Task.CompletedTask;
        }

        return RunExclusiveBehaviorAsync(
            token => RunClickBehaviorAsync(token),
            cancellationToken);
    }

    private async Task RunClickBehaviorAsync(CancellationToken cancellationToken)
    {
        foreach (PetBehaviorAction action in Definition.ClickBehavior)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (action.Action.Trim().ToLowerInvariant())
            {
                case "stop":
                    await StopMovingAsync();
                    break;
                case "play":
                    await PlayAnimationAsync(action.Animation ?? "roam");
                    break;
                case "wait":
                    await WaitAsync(
                        TimeSpan.FromSeconds(Math.Clamp(action.Seconds, 0.1, 10)),
                        cancellationToken);
                    break;
                case "resume":
                    await ResumePreviousBehaviorAsync();
                    break;
            }
        }
    }

    private async Task RunReminderBehaviorAsync(
        ReminderContext reminder,
        CancellationToken cancellationToken)
    {
        PetBehaviorAction[] actions = Definition.ReminderBehavior.Length == 0
            ? DefaultReminderBehavior
            : Definition.ReminderBehavior;

        foreach (PetBehaviorAction action in actions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (action.Action.Trim().ToLowerInvariant())
            {
                case "stop":
                    await StopMovingAsync();
                    break;
                case "play":
                    await PlayAnimationAsync(action.Animation ?? "roam");
                    break;
                case "show-message":
                    await ShowMessageAsync(reminder);
                    break;
                case "wait":
                    TimeSpan duration = action.Seconds > 0
                        ? TimeSpan.FromSeconds(Math.Clamp(action.Seconds, 0.1, 60))
                        : reminder.DisplayDuration;
                    await WaitAsync(duration, cancellationToken);
                    break;
                case "hide-message":
                    await HideMessageAsync();
                    break;
                case "resume":
                    await ResumePreviousBehaviorAsync();
                    break;
            }
        }
    }
}
