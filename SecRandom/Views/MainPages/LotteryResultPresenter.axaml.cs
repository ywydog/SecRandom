using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SecRandom.Helpers;
using SecRandom.ViewModels.MainPages;

namespace SecRandom.Views.MainPages;

/// <summary>Shared desktop/mobile projection of the desktop lottery result model.</summary>
public sealed partial class LotteryResultPresenter : UserControl
{
    private readonly ItemsControl _resultPresenter;
    private LotteryPageViewModel? _viewModel;
    private long _animationEpoch;

    public LotteryResultPresenter()
    {
        InitializeComponent();
        _resultPresenter = this.FindControl<ItemsControl>("ResultPresenter")!;
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) => AttachViewModel();
        DetachedFromVisualTree += (_, _) => DetachViewModel();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        AttachViewModel();
    }

    private void AttachViewModel()
    {
        var viewModel = DataContext as LotteryPageViewModel;
        if (ReferenceEquals(_viewModel, viewModel))
            return;

        DetachViewModel();
        _viewModel = viewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += ViewModelOnPropertyChanged;
    }

    private void DetachViewModel()
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
        _viewModel = null;
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var viewModel = _viewModel;
        if (viewModel is null)
            return;

        // 每次动画请求都会抬高动画世代号。延迟执行时若已有更新的请求（含停止后的结果动画）
        // 抢在前面，就丢弃本次过期帧，避免在停止时把最终结果当作滚动预览再播一次（闪一下）。
        var epoch = ++_animationEpoch;
        var isPreview = e.PropertyName == nameof(LotteryPageViewModel.PreviewAnimationRevision);
        var isResult = e.PropertyName == nameof(LotteryPageViewModel.ResultAnimationRevision);
        if (!isPreview && !isResult)
            return;

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render).GetTask();
                if (epoch != _animationEpoch)
                    return;

                if (isPreview)
                    await DrawAnimationHelper.PreviewAsync(_resultPresenter, viewModel.AnimationStyle,
                        viewModel.PreviewAnimationDuration);
                else if (isResult)
                    await DrawAnimationHelper.RevealAsync(_resultPresenter, viewModel.AnimationEnabled,
                        viewModel.AnimationStyle, viewModel.AnimationDuration);
            }
            catch
            {
                // A presentation animation must not affect the completed draw.
            }
        }, DispatcherPriority.Render);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
