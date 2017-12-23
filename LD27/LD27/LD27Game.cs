using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using System.Threading;
using System.IO;

namespace LD27
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class LD27Game : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        Room[,] Rooms = new Room[4,4];
        List<Door> Doors = new List<Door>();

        Hero gameHero;
        Camera gameCamera;

        EnemyController enemyController;
        ProjectileController projectileController;
        ParticleController particleController;
        PickupController pickupController;
        BombController bombController;

        BasicEffect drawEffect;

        SpriteFont font;
        SpriteFont timerFontLarge;
        SpriteFont timerFontSmall;
        Texture2D roomIcon;
        Texture2D texHud;
        Texture2D texTitle;
        Texture2D texTitleBG;
        Texture2D texStingers;

        MouseState lms;
        KeyboardState lks;
        GamePadState lgs;

        VoxelSprite tileSheet;
        VoxelSprite doorSheet;
        VoxelSprite objectSheet;

        int exitRoomX;
        int exitRoomY;

        Door exitDoor;

        Room currentRoom;

        int generatedPercent = 0;

        double doorCountdown = 0;
        double doorCountdownTarget = 10000; // Ten Seconds!

        double titleFrameTime = 0;
        int titleCurrentFrame = 0;
        Vector2 titleScrollPos;

        double deadTime;

        int roomMovesLeft = 0;
        RoomShift roomShift = null;

        RoomState roomState = RoomState.RoomsShifting;

        bool allRoomsComplete = false;

        bool shownComplete = false;
        double showCompleteTime = 0;
        float showCompleteAlpha = 0f;

        bool firstRun = true;    

        public LD27Game()
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
            graphics.PreferredBackBufferWidth = 1280;
            graphics.PreferredBackBufferHeight = 720;
            IsMouseVisible = true;
            graphics.ApplyChanges();

            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            generatedPercent = 0;
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            if (firstRun)
            {
                AudioController.LoadContent(Content);

                tileSheet = new VoxelSprite(16, 16, 16);
                LoadVoxels.LoadSprite(Path.Combine(Content.RootDirectory, "tiles.vxs"), ref tileSheet);
                doorSheet = new VoxelSprite(16, 16, 16);
                LoadVoxels.LoadSprite(Path.Combine(Content.RootDirectory, "door.vxs"), ref doorSheet);
                objectSheet = new VoxelSprite(16, 16, 16);
                LoadVoxels.LoadSprite(Path.Combine(Content.RootDirectory, "dynamic.vxs"), ref objectSheet);
            }
            else if (AudioController.instances["roomscrape"].State == SoundState.Playing) AudioController.instances["roomscrape"].Pause();

            gameCamera = new Camera(GraphicsDevice, GraphicsDevice.Viewport);
            particleController = new ParticleController(GraphicsDevice);
            projectileController = new ProjectileController(GraphicsDevice);
            pickupController = new PickupController(GraphicsDevice);
            bombController = new BombController(GraphicsDevice, objectSheet);
            enemyController = new EnemyController(GraphicsDevice);

            projectileController.LoadContent(Content);
            pickupController.LoadContent(Content);
            enemyController.LoadContent(Content);

            drawEffect = new BasicEffect(GraphicsDevice)
            {
                World = gameCamera.worldMatrix,
                View = gameCamera.viewMatrix,
                Projection = gameCamera.projectionMatrix,
                VertexColorEnabled = true,
            };

            gameHero = new Hero(0, 0, Vector3.Zero, Vector3.Zero);
            gameHero.LoadContent(Content, GraphicsDevice);

            ThreadPool.QueueUserWorkItem(delegate { CreateRoomsAsync(); });

            doorCountdown = 10000;
            roomMovesLeft = 0;
            roomShift = null;
            roomState = RoomState.DoorsOpen;
            deadTime = 0;
            allRoomsComplete = false;
            shownComplete = false;
            showCompleteTime = 0;
            showCompleteAlpha = 0f;

            Doors.Clear();
            Doors.Add(new Door(VoxelWorld.ToScreenSpace((7 * 16) + 7, 7, 21) + new Vector3(Voxel.HALF_SIZE,Voxel.HALF_SIZE,Voxel.HALF_SIZE), 0, doorSheet));
            Doors.Add(new Door(VoxelWorld.ToScreenSpace((14 * 16) + 7, (4 * 16) + 7, 21) + new Vector3(Voxel.HALF_SIZE, Voxel.HALF_SIZE, Voxel.HALF_SIZE), 1, doorSheet));
            Doors.Add(new Door(VoxelWorld.ToScreenSpace((7 * 16) + 7, (8 * 16) + 7, 21) + new Vector3(Voxel.HALF_SIZE, Voxel.HALF_SIZE, Voxel.HALF_SIZE), 2, doorSheet));
            Doors.Add(new Door(VoxelWorld.ToScreenSpace(7, (4 * 16) + 7, 21) + new Vector3(Voxel.HALF_SIZE, Voxel.HALF_SIZE, Voxel.HALF_SIZE), 3, doorSheet));

            if (firstRun)
            {
                roomIcon = Content.Load<Texture2D>("roomicon");
                texHud = Content.Load<Texture2D>("hud");
                texTitle = Content.Load<Texture2D>("titlesheet");
                texTitleBG = Content.Load<Texture2D>("title-bg");
                texStingers = Content.Load<Texture2D>("stingers");
                font = Content.Load<SpriteFont>("font");
                timerFontLarge = Content.Load<SpriteFont>("timerfont-large");
                timerFontSmall = Content.Load<SpriteFont>("timerfont-small");
            }

            firstRun = false;
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                this.Exit();

            if (generatedPercent >= 100)
            {
                MouseState cms = Mouse.GetState();
                KeyboardState cks = Keyboard.GetState();
                GamePadState cgs = GamePad.GetState(PlayerIndex.One);

                Vector2 mp2D = Vector2.Clamp(new Vector2(cms.X, cms.Y), Vector2.Zero, new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height));
                Vector3 mousePos = Helper.ProjectMousePosition(mp2D, GraphicsDevice.Viewport, gameCamera.worldMatrix, gameCamera.viewMatrix, gameCamera.projectionMatrix, 0f);

                Vector2 virtualJoystick = Vector2.Zero;
                if (cks.IsKeyDown(Keys.W) || cks.IsKeyDown(Keys.Up)) virtualJoystick.Y = -1;
                if (cks.IsKeyDown(Keys.A) || cks.IsKeyDown(Keys.Left)) virtualJoystick.X = -1;
                if (cks.IsKeyDown(Keys.S) || cks.IsKeyDown(Keys.Down)) virtualJoystick.Y = 1;
                if (cks.IsKeyDown(Keys.D) || cks.IsKeyDown(Keys.Right)) virtualJoystick.X = 1;
                if (virtualJoystick.Length() > 0f) virtualJoystick.Normalize();
                if (cgs.ThumbSticks.Left.Length() > 0.1f)
                {
                    virtualJoystick = cgs.ThumbSticks.Left;
                    virtualJoystick.Y = -virtualJoystick.Y;
                }

                if(gameHero.introTargetReached) gameHero.Move(virtualJoystick);

                if ((cks.IsKeyDown(Keys.Space) && !lks.IsKeyDown(Keys.Space)) || (cgs.Buttons.B == ButtonState.Pressed && lgs.Buttons.B != ButtonState.Pressed)) gameHero.TryPlantBomb(currentRoom);
                if (cks.IsKeyDown(Keys.Z) || cks.IsKeyDown(Keys.Enter) || cgs.Buttons.A == ButtonState.Pressed) gameHero.DoAttack();

                if (cks.IsKeyDown(Keys.X) || cks.IsKeyDown(Keys.RightShift) || cgs.Buttons.X == ButtonState.Pressed) gameHero.DoDefend(true, virtualJoystick); else gameHero.DoDefend(false, virtualJoystick);



                int openCount = 0;
                foreach (Door d in Doors) if (d.IsOpen) openCount++;

                if (gameHero.introTargetReached)
                {
                    #region ROOM STATE SHIT
                    switch (roomState)
                    {
                        case RoomState.DoorsOpening:
                            OpenDoors();
                            if (openCount > 0) roomState = RoomState.DoorsOpen;
                            doorCountdown = doorCountdownTarget;
                            break;
                        case RoomState.DoorsOpen:
                            if (doorCountdown > 0)
                            {
                                doorCountdown -= gameTime.ElapsedGameTime.TotalMilliseconds;

                                if (doorCountdown <= 0)
                                {
                                    roomState = RoomState.DoorsClosing;
                                }
                            }
                            break;
                        case RoomState.DoorsClosing:
                            foreach (Door d in Doors) d.Close(false);
                            if (openCount == 0)
                            {
                                roomMovesLeft = 3 + Helper.Random.Next(5);
                                DoRoomShift();
                                roomState = RoomState.RoomsShifting;
                            }
                            break;
                        case RoomState.RoomsShifting:
                            foreach (Door d in Doors) d.Close(true);
                            if (roomShift != null)
                            {
                                roomShift.Update(gameTime, gameHero, ref Rooms);
                                if (roomShift.Complete)
                                {
                                    if (roomMovesLeft > 0) DoRoomShift();
                                    else roomShift = null;
                                }
                            }
                            if (roomShift == null && roomMovesLeft == 0)
                            {
                                roomState = RoomState.DoorsOpening;
                            }
                            break;
                    }
                    #endregion
                }
                else
                {
                    if (Vector3.Distance(gameHero.Position, gameHero.IntroTarget) < 5f)
                    {
                        exitDoor.Close(false);
                    }
                }

                if (gameHero.RoomX == exitRoomX && gameHero.RoomY == exitRoomY)
                {
                    if (exitDoor.IsOpen)
                    {
                        particleController.Spawn(exitDoor.ParticlePosition, new Vector3(-0.05f + ((float)Helper.Random.NextDouble() * 0.1f), -0.05f + ((float)Helper.Random.NextDouble() * 0.1f), -0.05f + ((float)Helper.Random.NextDouble() * 0.1f)) + exitDoor.ParticleDir * 0.2f, 2f, Color.White * 0.5f, 1000, false);
                    }

                    
                }

                if (roomShift != null)
                    gameCamera.Update(gameTime, currentRoom.World, roomShift.cameraShake);
                else
                    gameCamera.Update(gameTime, currentRoom.World, Vector3.Zero);

                foreach (Room r in Rooms)
                    if (r.World != null) r.World.Update(gameTime, gameCamera, currentRoom == r);
                //currentRoom.World.Update(gameTime, gameCamera);

                gameHero.Update(gameTime, gameCamera, currentRoom, Doors, ref Rooms, allRoomsComplete, exitRoomX, exitRoomY, exitDoor);
                currentRoom = Rooms[gameHero.RoomX, gameHero.RoomY];
                currentRoom.Update(gameTime);

                enemyController.Update(gameTime, gameCamera, currentRoom, gameHero, Doors);
                particleController.Update(gameTime, gameCamera, currentRoom.World);
                pickupController.Update(gameTime, gameCamera, gameHero, currentRoom);
                projectileController.Update(gameTime, gameCamera, gameHero, currentRoom);
                bombController.Update(gameTime, currentRoom, gameHero);
                AudioController.Update(gameTime);

                foreach (Door d in Doors) d.Update(gameTime);

                drawEffect.View = gameCamera.viewMatrix;
                drawEffect.World = gameCamera.worldMatrix;

                lms = cms;
                lks = cks;
                lgs = cgs;

                if (gameHero.Dead || gameHero.exitReached)
                {
                    deadTime += gameTime.ElapsedGameTime.TotalMilliseconds;
                    if (deadTime >= 5000)
                    {
                        Reset();
                    }
                    if (showCompleteAlpha < 1f) showCompleteAlpha += 0.1f;
                    AudioController.StopMusic();

                }

                allRoomsComplete = true;
                foreach (Room r in Rooms) if (!r.IsComplete) allRoomsComplete = false;

                if (allRoomsComplete && !shownComplete)
                {
                    if (gameHero.RoomX == exitRoomX && gameHero.RoomY == exitRoomY && roomState == RoomState.DoorsOpen) exitDoor.Open(false);
                    if(showCompleteAlpha<1f) showCompleteAlpha += 0.1f;
                    showCompleteTime += gameTime.ElapsedGameTime.TotalMilliseconds;
                    if (showCompleteTime > 5000)
                    {
                        shownComplete = true;
                    }
                }
                if (shownComplete && showCompleteAlpha > 0f && !gameHero.exitReached) showCompleteAlpha -= 0.1f;
                //if (gameHero.exitReached && showCompleteAlpha < 1f) showCompleteAlpha += 0.1f;
                //if (gameHero.exitReached)
                //{
                //    dead
                //}
            }
            else
            {
                titleFrameTime += gameTime.ElapsedGameTime.TotalMilliseconds;
                if (titleFrameTime >= 100)
                {
                    titleFrameTime = 0;
                    titleCurrentFrame++;
                    if (titleCurrentFrame == 4) titleCurrentFrame = 0;
                }
                titleScrollPos += Vector2.One;
                if (titleScrollPos.X == texTitleBG.Width) titleScrollPos = Vector2.Zero;
            }

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            if (generatedPercent < 100)
            {
                spriteBatch.Begin();
                for(int x=0;x<4;x++)
                    for (int y = 0; y < 3; y++)
                    {
                        spriteBatch.Draw(texTitleBG, -titleScrollPos + (new Vector2(x * texTitleBG.Width, y * texTitleBG.Height)), null, Color.White);
                    }
                spriteBatch.End();

                GraphicsDevice.DepthStencilState = DepthStencilState.Default;
                GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
                GraphicsDevice.BlendState = BlendState.AlphaBlend;

                drawEffect.World =  Matrix.CreateRotationY(MathHelper.PiOver4) * Matrix.CreateRotationX(MathHelper.PiOver4);
                drawEffect.Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, GraphicsDevice.Viewport.AspectRatio, 0.1f, 200f);
                drawEffect.View = Matrix.CreateLookAt(new Vector3(0, 0, -20f), Vector3.Zero, Vector3.Down);

                foreach (EffectPass pass in drawEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();

                    AnimChunk c = gameHero.spriteSheet.AnimChunks[titleCurrentFrame];
                    GraphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalColor>(PrimitiveType.TriangleList, c.VertexArray, 0, c.VertexArray.Length, c.IndexArray, 0, c.VertexArray.Length / 2);
                    c = gameHero.spriteSheet.AnimChunks[titleCurrentFrame + 4];
                    GraphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalColor>(PrimitiveType.TriangleList, c.VertexArray, 0, c.VertexArray.Length, c.IndexArray, 0, c.VertexArray.Length / 2);

                }

                spriteBatch.Begin();
                spriteBatch.Draw(texTitle, new Vector2(0, 1), new Rectangle(0, 0, 1280, 20), Color.White);
                spriteBatch.Draw(texTitle, new Vector2(0, 1), new Rectangle(0, 20, (1280/100) * generatedPercent, 20), Color.White);
                spriteBatch.Draw(texTitle, new Vector2(300, (GraphicsDevice.Viewport.Height / 2)), new Rectangle(0, 40, 434, 380), Color.White, 0f, new Vector2(434, 380) / 2, 1f, SpriteEffects.None, 1);
                spriteBatch.Draw(texTitle, new Vector2(GraphicsDevice.Viewport.Width - 300, (GraphicsDevice.Viewport.Height / 2)), new Rectangle(433, 40, 393, 552), Color.White, 0f, new Vector2(393, 552) / 2, 1f, SpriteEffects.None, 1);

                //spriteBatch.DrawString(font, "generating: " + generatedPercent, new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height) / 2, Color.DarkGray, 0f, font.MeasureString("generating: " + generatedPercent) / 2, 1f, SpriteEffects.None, 1);
                spriteBatch.End();

                base.Draw(gameTime);
                return;                
            }
            
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
            GraphicsDevice.BlendState = BlendState.AlphaBlend;

            foreach (EffectPass pass in drawEffect.CurrentTechnique.Passes)
            {
                pass.Apply();

                for (int y = 0; y < currentRoom.World.Y_CHUNKS; y++)
                {
                    for (int x = 0; x < currentRoom.World.X_CHUNKS; x++)
                    {
                        Chunk c = currentRoom.World.Chunks[x, y, 0];
                        if (!c.Visible) continue;

                        if (c == null || c.VertexArray.Length == 0) continue;
                        if (!gameCamera.boundingFrustum.Intersects(c.boundingSphere)) continue;
                        GraphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalColor>(PrimitiveType.TriangleList, c.VertexArray, 0, c.VertexArray.Length, c.IndexArray, 0, c.VertexArray.Length / 2);
                    }
                }
            }

            currentRoom.Draw(GraphicsDevice, gameCamera, drawEffect);

            foreach (Door d in Doors) d.Draw(GraphicsDevice, gameCamera, drawEffect);
            enemyController.Draw(gameCamera, currentRoom);
            projectileController.Draw(gameCamera, currentRoom);
            pickupController.Draw(gameCamera, currentRoom);
            bombController.Draw(gameCamera, currentRoom);

            gameHero.Draw(GraphicsDevice, gameCamera);

            particleController.Draw();

            spriteBatch.Begin();
            //for (int x = 0; x < 4; x++)
            //    for (int y = 0; y < 4; y++)
            //    {
            //        if (!Rooms[x, y].IsGap) spriteBatch.Draw(roomIcon, new Vector2(5+(x*25), 5+(y*25)), null, (gameHero.RoomX==x && gameHero.RoomY==y)?Color.Magenta:Color.Gray);
            //    }
            spriteBatch.DrawString(timerFontLarge, ((int)(doorCountdown / 1000)).ToString("00"), new Vector2(GraphicsDevice.Viewport.Width-110, 5), Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None,1);
            double ms = doorCountdown - ((int)(doorCountdown / 1000) * 1000);
            spriteBatch.DrawString(timerFontSmall, MathHelper.Clamp(((float)ms/10f),0f,99f).ToString("00"), new Vector2(GraphicsDevice.Viewport.Width-35, 45), Color.Gray);
            spriteBatch.Draw(texHud, new Vector2(0, GraphicsDevice.Viewport.Height - texHud.Height), Color.White);
            //323,9 500,50
            //1139,9 30,50 15
            spriteBatch.Draw(texHud, new Vector2(0, GraphicsDevice.Viewport.Height - texHud.Height) + new Vector2(323, 9), new Rectangle(323, 9, (int)((500f / gameHero.MaxHealth) * gameHero.Health), 50), Color.Red);
            for (int i = 0; i < gameHero.numBombs; i++)
            {
                spriteBatch.Draw(texHud, new Vector2(0, GraphicsDevice.Viewport.Height - texHud.Height) + new Vector2(1139, 9) + new Vector2(i*45,0), new Rectangle(1139, 9, 30, 50), Color.Red);

            }

            if (showCompleteAlpha > 0f)
            {
                if (allRoomsComplete && !gameHero.exitReached)
                    spriteBatch.Draw(texStingers, new Vector2(GraphicsDevice.Viewport.Width / 2, 200), new Rectangle(0, 0, 413, 362), Color.White * showCompleteAlpha, 0f, new Vector2(413, 362) / 2, 1f, SpriteEffects.None, 1);
                else if (allRoomsComplete && gameHero.exitReached)
                    spriteBatch.Draw(texStingers, new Vector2(GraphicsDevice.Viewport.Width / 2, 200), new Rectangle(413, 0, 413, 362), Color.White * showCompleteAlpha, 0f, new Vector2(413, 362) / 2, 1f, SpriteEffects.None, 1);
                else if (gameHero.Dead)
                    spriteBatch.Draw(texStingers, new Vector2(GraphicsDevice.Viewport.Width / 2, 200), new Rectangle(413 * 2, 0, 413, 362), Color.White * showCompleteAlpha, 0f, new Vector2(413, 362) / 2, 1f, SpriteEffects.None, 1);

            }

            spriteBatch.End();

            base.Draw(gameTime);
        }

        void Reset()
        {
            AudioController.StopMusic();
            LoadContent();

        }

#region DOOR SHIT
        void OpenDoors()
        {
            if (gameHero.RoomX > 0 && !Rooms[gameHero.RoomX - 1, gameHero.RoomY].IsGap) Doors[3].Open(false);
            if (gameHero.RoomX < 3 && !Rooms[gameHero.RoomX + 1, gameHero.RoomY].IsGap) Doors[1].Open(false);
            if (gameHero.RoomY > 0 && !Rooms[gameHero.RoomX, gameHero.RoomY-1].IsGap) Doors[0].Open(false);
            if (gameHero.RoomY < 3 && !Rooms[gameHero.RoomX, gameHero.RoomY+1].IsGap) Doors[2].Open(false);

            if (allRoomsComplete && gameHero.RoomX == exitRoomX && gameHero.RoomY == exitRoomY) exitDoor.Open(false);

        }

        int prevRoomX = -1;
        int prevRoomY = -1;
        void DoRoomShift()
        {
            roomMovesLeft--;
            
            int gapX = 0;
            int gapY = 0;
            for(int x=0;x<4;x++)
                for(int y=0;y<4;y++)
                    if(Rooms[x,y].IsGap) { gapX = x; gapY = y; }

            int rx = 0;
            int ry = 0;
            bool ok = false;
            while(!ok)
            {
                rx = gapX-1 + Helper.Random.Next(3);
                ry = gapY-1 + Helper.Random.Next(3);
                if(!(rx==gapX&&ry==gapY) && (rx==gapX || ry==gapY) && !(rx==prevRoomX && ry==prevRoomY) && rx>=0 && rx<4 && ry>=0 && ry<4) ok=true;
            }

            roomShift = new RoomShift(rx, ry, gapX, gapY);
            prevRoomX = gapX;
            prevRoomY = gapY;
        }
#endregion

#region MAKIN ROOMS
        void CreateRoomsAsync()
        {
            int gapRoomX = Helper.Random.Next(4);
            int gapRoomY = Helper.Random.Next(4);

            float roomPercent = 100f / 16f;

            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                {
                    if (!(x == gapRoomX && y == gapRoomY))
                    {
                        Rooms[x, y] = new Room(tileSheet, objectSheet, false);
                    }
                    else Rooms[x, y] = new Room(tileSheet, objectSheet, true);
                    generatedPercent += (int)roomPercent;
                }

            // make exit
            bool made = false;
            while (!made)
            {
                int side = Helper.Random.Next(4);

                switch (side)
                {
                    case 0:
                        exitRoomX = Helper.Random.Next(4);
                        exitRoomY = -1;
                        if (!Rooms[exitRoomX, exitRoomY + 1].IsGap) made = true;
                        break;
                    case 1:
                        exitRoomY = Helper.Random.Next(4);
                        exitRoomX = 4;
                        if (!Rooms[exitRoomX - 1, exitRoomY].IsGap) made = true;
                        break;
                    case 2:
                        exitRoomX = Helper.Random.Next(4);
                        exitRoomY = 4;
                        if (!Rooms[exitRoomX, exitRoomY - 1].IsGap) made = true;
                        break;
                    case 3:
                        exitRoomY = Helper.Random.Next(4);
                        exitRoomX = -1;
                        if (!Rooms[exitRoomX + 1, exitRoomY].IsGap) made = true;
                        break;
                }
            }



            gameHero.RoomX = exitRoomX;// = new Hero(exitRoomX, exitRoomY, new Vector3(0, 0, 0));
            gameHero.RoomY = exitRoomY;

            //VoxelWorld.ToScreenSpace((7 * 16) + 7, 7, 21) + new Vector3(Voxel.HALF_SIZE,Voxel.HALF_SIZE,Voxel.HALF_SIZE), 0, doorSheet));
            ///VoxelWorld.ToScreenSpace((14 * 16) + 7, (4 * 16) + 7, 21) + new Vector3(Voxel.HALF_SIZE, Voxel.HALF_SIZE, Voxel.HALF_SIZE), 1, doorSheet));
            //VoxelWorld.ToScreenSpace((7 * 16) + 7, (8 * 16) + 7, 21) + new Vector3(Voxel.HALF_SIZE, Voxel.HALF_SIZE, Voxel.HALF_SIZE), 2, doorSheet));
            //VoxelWorld.ToScreenSpace(7, (4 * 16) + 7, 21) + new Vector3(Voxel.HALF_SIZE, Voxel.HALF_SIZE, Voxel.HALF_SIZE), 3, doorSheet));

            if (gameHero.RoomX == -1)
            {
                gameHero.RoomX = 0;
                exitRoomX = 0;
                gameHero.IntroTarget = Doors[3].Position + new Vector3(7f, 0f, 4f);
                gameHero.Position = Doors[3].Position + new Vector3(-7f, 0f, 4f);
                gameHero.Rotation = 0f;
                Doors[3].Open(true);
                exitDoor = Doors[3];
            }
            if (gameHero.RoomX == 4)
            {
                gameHero.RoomX = 3;
                exitRoomX = 3;
                gameHero.IntroTarget = Doors[1].Position + new Vector3(-7f, 0f, 4f);
                gameHero.Position = Doors[1].Position + new Vector3(7f, 0f, 4f);
                gameHero.Rotation = -MathHelper.Pi;
                Doors[1].Open(true);
                exitDoor = Doors[1];

            }
            if (gameHero.RoomY == -1)
            {
                gameHero.RoomY = 0;
                exitRoomY = 0;
                gameHero.IntroTarget = Doors[0].Position + new Vector3(0f, 7f, 4f);
                gameHero.Position = Doors[0].Position + new Vector3(0f, -7f, 4f);
                gameHero.Rotation = MathHelper.PiOver2;
                Doors[0].Open(true);
                exitDoor = Doors[0];

            }
            if (gameHero.RoomY == 4) 
            { 
                gameHero.RoomY = 3;
                exitRoomY = 3;
                gameHero.IntroTarget = Doors[2].Position + new Vector3(0f, -7f, 4f);
                gameHero.Position = Doors[2].Position + new Vector3(0f, 7f, 4f);
                gameHero.Rotation = -MathHelper.PiOver2;
                Doors[2].Open(true);
                exitDoor = Doors[2];

            }

            currentRoom = Rooms[gameHero.RoomX, gameHero.RoomY];

            enemyController.Enemies.RemoveAll(en => en.Room == currentRoom);

            gameCamera.Position = new Vector3((currentRoom.World.X_SIZE * Voxel.SIZE) / 2, (currentRoom.World.Y_SIZE * Voxel.SIZE) / 2, 0f);
            gameCamera.Target = new Vector3((currentRoom.World.X_SIZE * Voxel.SIZE) / 2, (currentRoom.World.Y_SIZE * Voxel.SIZE) / 2, 0f);

            generatedPercent = 100;

            OpenDoors();

            AudioController.PlayMusic("0");
        }
#endregion
    }

    enum RoomState
    {
        DoorsOpen,
        DoorsClosing,
        RoomsShifting,
        DoorsOpening
    }
}
