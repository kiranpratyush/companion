using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace HelloCompanion.App.Services;

internal sealed class PetSpeechBubble : IDisposable
{
    private const int BubbleWidth = 300;
    private const int BubbleHeight = 132;
    private readonly NativeSpriteWindow _window = new();
    private LayeredSpriteFrame? _frame;

    public void Show(string title, string message, PetScreenBounds petBounds)
    {
        using Bitmap bitmap = DrawBubble(title, message);
        LayeredSpriteFrame nextFrame = new((Bitmap)bitmap.Clone());
        DesktopBounds screen = DesktopGeometry.GetVirtualScreen();

        int x = petBounds.Left + ((petBounds.Width - BubbleWidth) / 2);
        x = Math.Clamp(x, screen.Left + 8, screen.Right - BubbleWidth - 8);

        int y = petBounds.Top - BubbleHeight + 12;
        if (y < screen.Top + 8)
        {
            y = petBounds.Bottom - 12;
        }

        y = Math.Clamp(y, screen.Top + 8, screen.Bottom - BubbleHeight - 8);
        _window.Render(nextFrame, x, y);

        LayeredSpriteFrame? previousFrame = _frame;
        _frame = nextFrame;
        previousFrame?.Dispose();
    }

    public void Hide() => _window.Hide();

    private static Bitmap DrawBubble(string title, string message)
    {
        Bitmap bitmap = new(BubbleWidth, BubbleHeight, PixelFormat.Format32bppPArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        Rectangle body = new(4, 4, BubbleWidth - 8, BubbleHeight - 22);
        using GraphicsPath bodyPath = CreateRoundedRectangle(body, 18);
        using SolidBrush background = new(Color.FromArgb(248, 255, 255, 255));
        using Pen outline = new(Color.FromArgb(210, 60, 60, 70), 2);
        graphics.FillPath(background, bodyPath);
        graphics.DrawPath(outline, bodyPath);

        Point[] tail =
        [
            new Point((BubbleWidth / 2) - 15, body.Bottom - 1),
            new Point(BubbleWidth / 2, BubbleHeight - 4),
            new Point((BubbleWidth / 2) + 15, body.Bottom - 1)
        ];
        graphics.FillPolygon(background, tail);
        graphics.DrawLines(outline, [tail[0], tail[1], tail[2]]);

        using Font titleFont = new("Segoe UI", 10.5f, FontStyle.Bold, GraphicsUnit.Point);
        using Font messageFont = new("Segoe UI", 12.5f, FontStyle.Regular, GraphicsUnit.Point);
        using SolidBrush titleBrush = new(Color.FromArgb(255, 70, 70, 82));
        using SolidBrush messageBrush = new(Color.FromArgb(255, 25, 25, 32));
        using StringFormat format = new()
        {
            Trimming = StringTrimming.EllipsisCharacter
        };

        graphics.DrawString(title, titleFont, titleBrush, new RectangleF(20, 16, BubbleWidth - 40, 24), format);
        graphics.DrawString(message, messageFont, messageBrush, new RectangleF(20, 43, BubbleWidth - 40, 60), format);
        return bitmap;
    }

    private static GraphicsPath CreateRoundedRectangle(Rectangle rectangle, int radius)
    {
        int diameter = radius * 2;
        GraphicsPath path = new();
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public void Dispose()
    {
        _window.Dispose();
        _frame?.Dispose();
    }
}

internal readonly record struct PetScreenBounds(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
}
