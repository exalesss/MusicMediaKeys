using System.Linq;
using Xunit;

namespace MusicMediaKeys.Tests
{
    public class HotkeyRegistrationPlanServiceTests
    {
        [Fact]
        public void Build_ReturnsExpectedActions()
        {
            AppSettings settings = new AppSettings();

            var plan = HotkeyRegistrationPlanService.Build(settings);

            Assert.Equal(6, plan.Count);
            Assert.Contains(plan, item => item.ActionId == AppActions.Previous && item.Command == MediaCommand.Previous);
            Assert.Contains(plan, item => item.ActionId == AppActions.VolumeDown && item.Command == MediaCommand.VolumeDown);
        }

        [Fact]
        public void Build_IncludesCustomActionHotkeysAsAppAction()
        {
            AppSettings settings = new AppSettings();
            settings.CustomActionHotkeys.Add(new ActionHotkeyBinding
            {
                ActionId = AppActions.ToggleMainWindow,
                Binding = new HotkeyBinding { Key = 77, Modifiers = (uint)(ModifierKeys.Control | ModifierKeys.Alt) }
            });

            var plan = HotkeyRegistrationPlanService.Build(settings);

            Assert.Equal(7, plan.Count);
            var customItem = plan.FirstOrDefault(item => item.ActionId == AppActions.ToggleMainWindow);
            Assert.NotNull(customItem);
            Assert.Equal(HotkeyRegistrationKind.AppAction, customItem.Kind);
            Assert.NotNull(customItem.Binding);
            Assert.Equal(77, customItem.Binding!.Key);
        }

        [Fact]
        public void Build_HandlesCustomActions()
        {
            AppSettings settings = new AppSettings();
            settings.CustomActionHotkeys.Add(new ActionHotkeyBinding
            {
                ActionId = "",
                Binding = new HotkeyBinding { Key = 77, Modifiers = 1 }
            });
            settings.CustomActionHotkeys.Add(new ActionHotkeyBinding
            {
                ActionId = AppActions.OpenHotkeysTab,
                Binding = null
            });

            var plan = HotkeyRegistrationPlanService.Build(settings);

            Assert.Equal(7, plan.Count);
            var item = plan.FirstOrDefault(x => x.ActionId == AppActions.OpenHotkeysTab);
            Assert.NotNull(item);
            Assert.Null(item.Binding);
            Assert.Equal(HotkeyRegistrationKind.AppAction, item.Kind);
        }
    }
}
