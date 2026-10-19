using MudBlazor;

namespace Relewise.BlazorApps.Layout;

public static class AppTheme
{
    public static MudTheme Relewise { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#3764E4",
            Secondary = "#2152C1",
            Success = "#4DD174",
            SuccessContrastText = "#292929",
            Warning = "#FFD93B",
            WarningContrastText = "#292929",
            Info = "#3764E4",
            Background = "#FFFFFF",
            Surface = "#FFFFFF",
            TextPrimary = "#292929",
            AppbarBackground = "#3764E4",
            AppbarText = "#FFFFFF",
            DrawerBackground = "#F3F7FD",
            DrawerText = "#292929",
            DrawerIcon = "#3764E4",
            Divider = "#D9D9D9"
        },
        LayoutProperties = new LayoutProperties
        {
            DrawerWidthLeft = "260px"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Trebuchet MS", "Arial", "sans-serif"]
            },
            Button = new ButtonTypography
            {
                TextTransform = "none",
                LetterSpacing = "normal"
            }
        }
    };
}
