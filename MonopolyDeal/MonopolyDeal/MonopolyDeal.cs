using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;


namespace MonopolyDeal
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class MonopolyDeal : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        // This is a texture we can render.
        Texture2D myTexture;
        Texture2D myTexture2;

        // Set the coordinates to draw the sprite at.
        Vector2 spritePosition = Vector2.Zero;

        // Create the deck
        Deck deck = new Deck(); 

        public MonopolyDeal()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
            IsMouseVisible = true;

            // Create a player
            Player player1 = new Player(deck, "Player 1");
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            myTexture = Content.Load<Texture2D>("cardback");
            myTexture2 = Content.Load<Texture2D>("10million");
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        // Store some information about the sprite's motion.

        protected override void Update(GameTime gameTime)
        {
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back ==
                Microsoft.Xna.Framework.Input.ButtonState.Pressed)
                this.Exit();

            // Move the sprite around.
            UpdateSprite(gameTime);

            base.Update(gameTime);
        }

        // Right now, this method allows the user to move the sprite around the window with the arrow keys.
        void UpdateSprite(GameTime gameTime)
        {
            KeyboardState keyboardState = Keyboard.GetState();

            if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Right))
            {
                spritePosition.X++;
            }
            if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Left))
            {
                spritePosition.X--;
            }
            if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Up))
            {
                spritePosition.Y--;
            }
            if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Down))
            {
                spritePosition.Y++;
            }

            // ROBIN: Pop up a message box if the user clicks on the deck's initial position (represented by the picture of the back
            // of a card). This is a test to see how click events can be handled in XNA.
            // By the way, I'm starting not to like XNA; consider switching to XAML.

            //TYLER: You can now move the card around and the click still works with its new position.
            MouseState mouseState = Mouse.GetState();
            Rectangle scaledBounds = new Rectangle((int)(spritePosition.X), (int)(spritePosition.Y), (int)(myTexture.Width * 0.3), (int)(myTexture.Height * 0.3));
            if (scaledBounds.Contains(new Point(mouseState.X, mouseState.Y)) && mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {
                MessageBox.Show("You have clicked the deck.");

                //spriteBatch.Begin();
                //spriteBatch.Draw(myTexture2, new Vector2(150, 150), null, Color.White, 0, new Vector2(), .3f, SpriteEffects.None, 0);
                //spriteBatch.End();
            }
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            graphics.GraphicsDevice.Clear(Color.CornflowerBlue);

            // Draw the sprite.
            spriteBatch.Begin();
            spriteBatch.Draw(myTexture, spritePosition, null, Color.White, 0, new Vector2(), .3f, SpriteEffects.None, 0);
            spriteBatch.Draw(myTexture2, new Vector2(150, 150), null, Color.White, 0, new Vector2(), .3f, SpriteEffects.None, 0);
            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
