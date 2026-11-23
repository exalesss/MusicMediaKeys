using Xunit;

namespace MusicMediaKeys.Tests
{
    public class TrayStatusServiceTests
    {
        [Fact]
        public void BuildTrayText_ForAutoMode_UsesActiveApplicationName()
        {
            AppSettings settings = new AppSettings
            {
                SelectedApplication = ApplicationSelectionMode.Auto,
                UseSystemMediaKeysOnly = false
            };

            string text = TrayStatusService.BuildTrayText(settings, isRunning: true, activeApplication: MediaApplication.YandexMusic);

            Assert.Contains(Localization.GetString("Auto"), text);
            Assert.Contains(Localization.GetString("YandexMusic"), text);
            Assert.Contains(Localization.GetString("AppActive"), text);
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(true, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, true, true)]
        public void ShouldUseActiveIcon_MatchesHealthState(bool isRunning, bool anyAppMode, bool expected)
        {
            Assert.Equal(expected, TrayStatusService.ShouldUseActiveIcon(isRunning, anyAppMode));
        }

        [Fact]
        public void BuildTrayText_WhenHotkeysSuspended_ShowsPausedStatusAndErrorIcon()
        {
            AppSettings settings = new AppSettings();
            string text = TrayStatusService.BuildTrayText(settings, isRunning: true, activeApplication: MediaApplication.Spotify, hotkeysSuspended: true);

            Assert.Contains(Localization.GetString("TrayHotkeysSuspended"), text);
            Assert.False(TrayStatusService.ShouldUseActiveIcon(isRunning: true, anyAppMode: true, hotkeysSuspended: true));
        }

        [Fact]
        public void AppActions_IncludesPhase2CustomActions()
        {
            Assert.Contains(AppActions.ToggleVolumeDucking, AppActions.AvailableCustomActions);
            Assert.Contains(AppActions.FocusTargetPlayer, AppActions.AvailableCustomActions);
            Assert.Contains(AppActions.ToggleHotkeysSuspended, AppActions.AvailableCustomActions);
        }

        [Fact]
        public void TrayContextMenu_PositionsTopLeftOfCursor()
        {
            System.Exception? threadEx = null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    var menu = TrayContextMenuService.CreateContextMenu(Theme.Dark, () => { }, () => { }, () => { });
                    Assert.NotNull(menu);
                    Assert.Equal(System.Windows.Controls.Primitives.PlacementMode.MousePoint, menu.Placement);
                    Assert.True(menu.HorizontalOffset < 0);
                    Assert.True(menu.VerticalOffset < 0);
                    Assert.Equal(-menu.DesiredSize.Width, menu.HorizontalOffset);
                    Assert.Equal(-menu.DesiredSize.Height, menu.VerticalOffset);
                    Assert.Equal(4, menu.Items.Count);

                    var richMenu = TrayContextMenuService.CreateContextMenu(
                        Theme.Dark,
                        () => { },
                        () => { },
                        () => { },
                        new TrayMenuOptions
                        {
                            SelectedApplication = ApplicationSelectionMode.Auto,
                            OnCyclePlayer = () => (ApplicationSelectionMode.Auto, "")
                        });
                    Assert.Equal(5, richMenu.Items.Count);
                    Assert.True(((System.Windows.Controls.MenuItem)richMenu.Items[2]).StaysOpenOnClick);
                }
                catch (System.Exception ex)
                {
                    threadEx = ex;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (threadEx != null) throw threadEx;
        }
    }
}
