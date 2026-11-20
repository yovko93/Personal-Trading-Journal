using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Views.Calendar;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

public sealed partial class CalendarDayModalTests
{
    [Fact]
    public async Task NativeThemeResourcesReuseUnchangedStylesOnTheirOwningStaWithDistinctThemes()
    {
        await OnSta(() =>
        {
            var light = CalendarViewLayoutTests.SharedThemeResources("Light");
            var dark = CalendarViewLayoutTests.SharedThemeResources("Dark");
            Assert.Same(light, CalendarViewLayoutTests.SharedThemeResources("Light"));
            Assert.Same(dark, CalendarViewLayoutTests.SharedThemeResources("Dark"));
            Assert.NotSame(light, dark);
            Assert.NotSame(light["PtjButtonStyle"], dark["PtjButtonStyle"]);
            Assert.NotEqual(((SolidColorBrush)light["PtjBackgroundBrush"]).Color,
                ((SolidColorBrush)dark["PtjBackgroundBrush"]).Color);
            foreach (var resources in new[] { light, dark })
            {
                var style = Assert.IsType<Style>(resources["PtjButtonStyle"]);
                // WPF detaches a sealed Style from its dispatcher so it can be shared.
                // An unsealed style must still belong to this STA.
                Assert.True(style.CheckAccess());
                if (!style.IsSealed) Assert.Same(System.Windows.Threading.Dispatcher.CurrentDispatcher, style.Dispatcher);
                var first = new Button { Resources = resources, Style = style };
                var second = new Button { Resources = resources, Style = style };
                first.Measure(new Size(200, 60)); second.Measure(new Size(200, 60));
                Assert.Same(first.Style, second.Style);
                Assert.Same(resources["PtjBackgroundBrush"], CalendarViewLayoutTests.SharedThemeResources(
                    ReferenceEquals(resources, light) ? "Light" : "Dark")["PtjBackgroundBrush"]);
            }
            Task.Run(() => Assert.Throws<InvalidOperationException>(() =>
                CalendarViewLayoutTests.SharedThemeResources("Light"))).GetAwaiter().GetResult();
        });
    }
}
