namespace MonoGameSample.Samples
{
    //public class RenderContextTest(Game game) : DrawableGameComponent(game)
    //{
    //    private RenderContext renderContext;
    //    private FontAdapter font;
    //    private SolidColorBrush solidBrush;
    //    private ImageBrush imageBrush;
    //    private InputSystem input;

    //    private IBrush current;
    //    private Rectangle source;
    //    private Rectangle drawArea;
    //    private float rotation;
    //    private Vector2 origin;
    //    private Color drawColor;

    //    public override void Initialize()
    //    {
    //        renderContext = new(GraphicsDevice);
    //        base.Initialize();
    //    }

    //    protected override void LoadContent()
    //    {
    //        font = new(Game.Content.Load<SpriteFont>("Segoe UI"));
    //        solidBrush = new(Color.White.AsSystemColor());
    //        imageBrush = new(new TextureAdapter(Game.Content.Load<Texture2D>("bobr")));
    //        current = solidBrush;
    //        input = new(Game);

    //        drawArea.Location = (Game.Window.ClientBounds.Size.ToVector2() * 0.5f).ToPoint();
    //        drawArea.Width = drawArea.Height = 200;
    //        source = imageBrush.DrawArea.AsEngineRectangle();
    //        drawColor = Color.White;

    //        base.LoadContent();
    //    }

    //    public override void Update(GameTime gameTime)
    //    {
    //        input.Update(gameTime.ElapsedGameTime);
    //        foreach (var key in input.Keyboard.KeysDown)
    //        {
    //            var mod = input.Keyboard.ModifierKeys;
    //            switch (key)
    //            {
    //                case Keys.Left when mod == ModifierKeys.Ctrl:
    //                    origin.X--; break;
    //                case Keys.Left when mod == ModifierKeys.Alt:
    //                    drawArea.Width--; break;
    //                case Keys.Left when mod == ModifierKeys.Shift:
    //                    source.X--; break;
    //                case Keys.Left when mod == (ModifierKeys.Ctrl | ModifierKeys.Shift):
    //                    source.Width--; break;
    //                case Keys.Left:
    //                    drawArea.X--; break;

    //                case Keys.Right when mod == ModifierKeys.Ctrl:
    //                    origin.X++; break;
    //                case Keys.Right when mod == ModifierKeys.Alt:
    //                    drawArea.Width++; break;
    //                case Keys.Right when mod == ModifierKeys.Shift:
    //                    source.X++; break;
    //                case Keys.Right when mod == (ModifierKeys.Ctrl | ModifierKeys.Shift):
    //                    source.Width++; break;
    //                case Keys.Right:
    //                    drawArea.X++; break;

    //                case Keys.Up when mod == ModifierKeys.Ctrl:
    //                    origin.Y--; break;
    //                case Keys.Up when mod == ModifierKeys.Alt:
    //                    drawArea.Height--; break;
    //                case Keys.Up when mod == ModifierKeys.Shift:
    //                    source.Y--; break;
    //                case Keys.Up when mod == (ModifierKeys.Ctrl | ModifierKeys.Shift):
    //                    source.Height--; break;
    //                case Keys.Up:
    //                    drawArea.Y--; break;

    //                case Keys.Down when mod == ModifierKeys.Ctrl:
    //                    origin.Y++; break;
    //                case Keys.Down when mod == ModifierKeys.Alt:
    //                    drawArea.Height++; break;
    //                case Keys.Down when mod == ModifierKeys.Shift:
    //                    source.Y++; break;
    //                case Keys.Down when mod == (ModifierKeys.Ctrl | ModifierKeys.Shift):
    //                    source.Height++; break;
    //                case Keys.Down:
    //                    drawArea.Y++; break;

    //                case Keys.R:
    //                    if (mod == ModifierKeys.Shift)
    //                    {
    //                        rotation -= (float)gameTime.ElapsedGameTime.TotalSeconds;
    //                    }
    //                    else
    //                    {
    //                        rotation += (float)gameTime.ElapsedGameTime.TotalSeconds;
    //                    }
    //                    break;

    //                case Keys.I:
    //                    if (current == solidBrush)
    //                        current = imageBrush;
    //                    else
    //                        current = solidBrush;
    //                    break;

    //                case Keys.D0:
    //                    drawColor = Color.White; break;
    //                case Keys.D1:
    //                    drawColor = Color.Red; break;
    //                case Keys.D2:
    //                    drawColor = Color.Green; break;
    //                case Keys.D3:
    //                    drawColor = Color.Blue; break;
    //                case Keys.D4:
    //                    drawColor = Color.Cyan; break;
    //                case Keys.D5:
    //                    drawColor = Color.Magenta; break;
    //                case Keys.D6:
    //                    drawColor = Color.Yellow; break;
    //                case Keys.D7:
    //                    drawColor = Color.Gray; break;
    //                case Keys.D8:
    //                    drawColor = Color.Violet; break;
    //                case Keys.D9:
    //                    drawColor = Color.Orange; break;
    //            }
    //        }
    //        base.Update(gameTime);
    //    }

    //    public override void Draw(GameTime gameTime)
    //    {
    //        renderContext.Begin();
    //        // Background
    //        renderContext.Draw(imageBrush, new(300, 20, 400, 300), Color.White.AsSystemColor());
    //        // Target
    //        renderContext.Draw(
    //            current,
    //            drawArea.AsSystemRectangle(),
    //            source.AsSystemRectangle(),
    //            drawColor.AsSystemColor(),
    //            rotation,
    //            origin.AsSystemVector());
    //        // Border
    //        renderContext.DrawRectangle(drawArea.AsSystemRectangle(), Color.Red.AsSystemColor());
    //        // Foreground
    //        renderContext.Draw(solidBrush, new(20, 300, 100, 100), Color.Magenta.AsSystemColor());
    //        // Hint text
    //        renderContext.DrawString(
    //            font,
    //            "Use arrows to move the rectangle. Controls:\n" +
    //            "Ctrl - move origin point,\n" +
    //            "Alt - size,\n" +
    //            "Shift - move source rectangle,\n" +
    //            "Ctrl+Shift - size source rectangle,\n\n" +
    //            "Numbers - change color,\n" +
    //            "I - switch image brush,\n" +
    //            "R - rotate.",
    //            new(700, 200),
    //            Color.Black.AsSystemColor());
    //        renderContext.End();
    //        base.Draw(gameTime);
    //    }
    //}
}
