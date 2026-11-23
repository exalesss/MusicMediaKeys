using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MusicMediaKeys
{
    public partial class UpdateControl : UserControl
    {
        private enum State { Idle, Checking, UpToDate, CheckFailed, RateLimited, Available, Downloading, Installed, InstallFailed }

        private static readonly TimeSpan AnswerShownFor = TimeSpan.FromSeconds(3);
        private readonly DispatcherTimer _answerTimer = new() { Interval = AnswerShownFor };

        private State _state;
        private Version? _installedVersion;
        private string? _error;
        private double _progress;
        private CancellationTokenSource? _download;

        public UpdateControl()
        {
            InitializeComponent();
            _answerTimer.Tick += (s, e) =>
            {
                _answerTimer.Stop();
                if (_state is State.UpToDate or State.CheckFailed or State.RateLimited)
                    SetState(UpdateService.Available != null ? State.Available : State.Idle);
            };
            Loaded += (s, e) =>
            {
                UpdateService.AvailableChanged += UpdateService_AvailableChanged;
                Localization.LanguageChanged += Localization_LanguageChanged;
                UpdateService_AvailableChanged();
            };
            Unloaded += (s, e) =>
            {
                UpdateService.AvailableChanged -= UpdateService_AvailableChanged;
                Localization.LanguageChanged -= Localization_LanguageChanged;
            };
        }

        private void UpdateService_AvailableChanged()
        {
            if (_state is State.Checking or State.Downloading or State.Installed or State.InstallFailed)
                return;

            SetState(UpdateService.Available != null ? State.Available : (_state == State.Available ? State.Idle : _state));
        }

        private void Localization_LanguageChanged(object? sender, EventArgs e) => ShowState();

        private async void BtnAction_Click(object sender, RoutedEventArgs e)
        {
            switch (_state)
            {
                case State.Available:
                case State.InstallFailed:
                    await InstallAsync();
                    break;
                case State.Downloading:
                    _download?.Cancel();
                    break;
                case State.Installed:
                    Restart();
                    break;
                default:
                    await CheckAsync();
                    break;
            }
        }

        private async Task<UpdateCheckResult> CheckAsync()
        {
            SetState(State.Checking);
            UpdateCheckResult result = await UpdateService.CheckAsync();
            SetState(result switch
            {
                UpdateCheckResult.Available => State.Available,
                UpdateCheckResult.UpToDate => State.UpToDate,
                UpdateCheckResult.RateLimited => State.RateLimited,
                _ => State.CheckFailed
            });
            return result;
        }

        private async Task InstallAsync()
        {
            if (await CheckAsync() == UpdateCheckResult.UpToDate || UpdateService.Available is not UpdateInfo update)
                return;

            _download = new CancellationTokenSource();
            _progress = 0;
            SetState(State.Downloading);
            try
            {
                await UpdateService.InstallAsync(update, new Progress<double>(ShowProgress), _download.Token);
                _installedVersion = update.Version;
                SetState(State.Installed);
            }
            catch (OperationCanceledException)
            {
                SetState(State.Available);
            }
            catch (Exception ex)
            {
                EventLogService.Log("Update", "Update failed: " + ex.Message);
                _error = ex.Message;
                SetState(State.InstallFailed);
            }
            finally
            {
                _download.Dispose();
                _download = null;
            }
        }

        private static void Restart()
        {
            try
            {
                UpdateService.StartUpdatedInstance();
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                EventLogService.Log("Update", "Restart failed: " + ex.Message);
            }
        }

        private void ShowProgress(double fraction)
        {
            _progress = Math.Clamp(fraction, 0, 1);
            ProgressDone.Width = new GridLength(_progress, GridUnitType.Star);
            ProgressLeft.Width = new GridLength(1 - _progress, GridUnitType.Star);
            TxtStatus.Text = string.Format(Localization.GetString("UpdateStatusDownloading"), (int)(_progress * 100));
        }

        private void SetState(State state)
        {
            _state = state;
            _answerTimer.Stop();
            if (state is State.UpToDate or State.CheckFailed or State.RateLimited)
                _answerTimer.Start();
            ShowState();
        }

        private void ShowState()
        {
            string? rawVersion = (_state == State.Installed ? _installedVersion : UpdateService.Available?.Version) is Version v
                ? UpdateService.FormatVersion(v)
                : null;
            string? version = rawVersion;
            bool highlight = _state is State.Available or State.Installed;

            TxtStatus.Text = _state switch
            {
                State.Checking => Localization.GetString("UpdateStatusChecking"),
                State.UpToDate => Localization.GetString("UpdateStatusUpToDate"),
                State.CheckFailed => Localization.GetString("UpdateStatusNoConnection"),
                State.RateLimited => Localization.GetString("UpdateStatusTryLater"),
                State.Available => string.Format(Localization.GetString("UpdateStatusAvailable"), version),
                State.Installed => string.Format(Localization.GetString("UpdateStatusReady"), version),
                State.InstallFailed => Localization.GetString("UpdateStatusFailed"),
                State.Downloading => string.Format(Localization.GetString("UpdateStatusDownloading"), (int)(_progress * 100)),
                _ => string.Format(Localization.GetString("UpdateStatusVersion"), AppInfo.Version)
            };
            if (highlight)
                TxtStatus.SetResourceReference(TextBlock.ForegroundProperty, "TealAccent");
            else
                TxtStatus.SetResourceReference(TextBlock.ForegroundProperty, "TextGray");
            TxtStatus.FontWeight = highlight ? FontWeights.SemiBold : FontWeights.Normal;

            ProgressTrack.Visibility = _state == State.Downloading ? Visibility.Visible : Visibility.Collapsed;

            BtnAction.Content = Localization.GetString(_state switch
            {
                State.Available => "UpdateInstall",
                State.InstallFailed => "Retry",
                State.Downloading => "Cancel",
                State.Installed => "UpdateRestart",
                _ => "UpdateCheck"
            });
            BtnAction.IsEnabled = _state != State.Checking;
            BtnAction.SetResourceReference(StyleProperty, highlight ? "PrimaryButton" : "SecondaryButton");

            CursorFollowToolTip.SetText(this, _state switch
            {
                State.CheckFailed => Localization.GetString("UpdateCheckFailed"),
                State.RateLimited => Localization.GetString("TooltipUpdateRateLimited"),
                State.Installed => Localization.GetString("TooltipUpdateReady"),
                State.InstallFailed => _error,
                _ => null
            });
        }
    }
}
