using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace MatchThreeEngine
{
    public sealed class Board : MonoBehaviour
    {
        [SerializeField] private TileTypeAsset[] tileTypes;

        [SerializeField] private Row[] rows;

        [SerializeField] private AudioClip matchSoundClip;

        [SerializeField] private AudioSource audioSourceComponent;

        [SerializeField] private float tileTweenDuration;

        [SerializeField] private Transform swapOverlayTransform;

        [SerializeField] private bool noStartingMatches;
        [SerializeField] private float maxGameDuration = 100f;

        private readonly List<Tile> selectedTilesList = new List<Tile>();

        private bool isTileSwapping;
        public GameObject eventGameObject;
        private bool isTileMatching;
        private bool isBoardShuffling;
        private bool isGameActive; 
        public int playerScore;
        public TMP_Text scoreTextComponent;
        public TMP_Text winScoreText;
        public TMP_Text loseScoreText;
        public TMP_Text timerTextComponent;
        public GameObject winPanelObject;
        public GameObject losePanelObject;
        private float remainingTime;
        private Coroutine countdownCoroutine;
        public int currentLevelIndex = 1;
        public Button[] levelButtons;
        public int totalLevels = 1;
        public GameObject levelSelectionMenu;
        public bool vibrationEnabled;

        public event Action<TileTypeAsset, int> OnMatch;

        private TileData[,] Matrix
        {
            get
            {
                var width = rows.Max(row => row.tiles.Length);
                var height = rows.Length;

                var data = new TileData[width, height];

                for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                        data[x, y] = GetTile(x, y).Data;

                return data;
            }
        }

        //handle level selection and starting the game
        public void ButtonLevelSelect(int levelSelected)
        {
            currentLevelIndex = levelSelected;
            levelSelectionMenu.SetActive(false);
            StartGame();
        }

        // quit the game
        public void QuitGame()
        {
            Application.Quit();
        }

        // Method to save the completed levels
        void SaveLevels()
        {
            PlayerPrefs.SetInt("LevelsCompleted", totalLevels);
            PlayerPrefs.Save();
        }

        // Method called at the start of the game to load levels and update button interactivity
        void Start()
        {
            LoadLevels();
            UpdateButtonInteractivity();
        }

        // Method to advance to the next level
        public void NextLevel()
        {
            if (currentLevelIndex < 25)
            {
                currentLevelIndex += 1;
            }
            StartGame();
        }

        // Method to load the levels from player preferences
        void LoadLevels()
        {
            totalLevels = PlayerPrefs.GetInt("LevelsCompleted", 1);
        }

        // Method to update the interactivity of level selection buttons
        void UpdateButtonInteractivity()
        {
            for (int i = 0; i < levelButtons.Length; i++)
            {
                levelButtons[i].interactable = i < totalLevels;
            }
        }

        // Method to start the game and initialize the board
        public void StartGame()
        {
            for (var y = 0; y < rows.Length; y++)
            {
                for (var x = 0; x < rows.Max(row => row.tiles.Length); x++)
                {
                    var tile = GetTile(x, y);

                    tile.x = x;
                    tile.y = y;

                    tile.Type = tileTypes[Random.Range(0, tileTypes.Length)];

                    tile.button.onClick.AddListener(() => Select(tile));
                }
            }
            playerScore = 0;
            scoreTextComponent.text = "Score: " + playerScore + "/700";
            if (noStartingMatches) StartCoroutine(EnsureNoStartingMatches());
            StopTimer();
            StartTimer();
            remainingTime = maxGameDuration;
            isGameActive = true;
            countdownCoroutine = StartCoroutine(TimerCoroutine());
        }

        // Coroutine to handle the game timer countdown
        private IEnumerator TimerCoroutine()
        {
            while (remainingTime > 0 && isGameActive)
            {
                remainingTime -= Time.deltaTime;
                int seconds = (int)remainingTime;
                timerTextComponent.text = "Time: " + seconds + "s"; // Update timer text

                if (remainingTime <= 0)
                {
                    if (playerScore >= 700)
                    {
                        WinScenario();
                    }
                    else
                    {
                        GameLost();
                    }
                }

                yield return null;
            }
        }

        // Method to stop the game timer
        public void StopTimer()
        {
            isGameActive = false;
            if (countdownCoroutine != null)
            {
                StopCoroutine(countdownCoroutine);
            }
        }

        // Method to start the game timer
        public void StartTimer()
        {
            isGameActive = true;
            countdownCoroutine = StartCoroutine(TimerCoroutine());
        }

        // Method to handle game win scenario
        void WinScenario()
        {
            if (vibrationEnabled)
            {
                Handheld.Vibrate();
            }
            winScoreText.text = "Score: " + playerScore + "/700";
            winPanelObject.SetActive(true);

            StopTimer();
        }

        public void AfterWin() //win
        {
            if (totalLevels < 25)
            {
                totalLevels++;
            }
            SaveLevels();
            UpdateButtonInteractivity();
        }

        public void falseVibr()
        {
            vibrationEnabled = false;
        }

        public void trueVibr()
        {
            vibrationEnabled = true;
        }

        void GameLost()  //lost
        {
            if (vibrationEnabled)
            {
                Handheld.Vibrate();
            }
            loseScoreText.text = "Score: " + playerScore + "/700";
            losePanelObject.SetActive(true);
            StopTimer();
        }

        // handle updates in the game, specifically for checking user input
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                var bestMove = TileDataMatrixUtility.FindBestMove(Matrix);

                if (bestMove != null)
                {
                    Select(GetTile(bestMove.X1, bestMove.Y1));
                    Select(GetTile(bestMove.X2, bestMove.Y2));
                }
            }
        }

        // ensure no starting matches on the board
        private IEnumerator EnsureNoStartingMatches()
        {
            var wait = new WaitForEndOfFrame();

            while (TileDataMatrixUtility.FindBestMatch(Matrix) != null)
            {
                Shuffle();

                yield return wait;
            }
        }

        // Method to get a specific tile at given coordinates
        private Tile GetTile(int x, int y) => rows[y].tiles[x];

        // Method to get tiles from a list of tile data
        private Tile[] GetTiles(IList<TileData> tileData)
        {
            var length = tileData.Count;

            var tiles = new Tile[length];

            for (var i = 0; i < length; i++) tiles[i] = GetTile(tileData[i].X, tileData[i].Y);

            return tiles;
        }

        // handle tile selection and swapping logic
        private async void Select(Tile tile)
        {
            if (isTileSwapping || isTileMatching || isBoardShuffling)
            {
                Debug.Log("Action in progress, selection ignored.");
                return;
            }

            if (!selectedTilesList.Contains(tile))
            {
                if (selectedTilesList.Count > 0)
                {
                    if (Math.Abs(tile.x - selectedTilesList[0].x) == 1 && Math.Abs(tile.y - selectedTilesList[0].y) == 0
                        || Math.Abs(tile.y - selectedTilesList[0].y) == 1 && Math.Abs(tile.x - selectedTilesList[0].x) == 0)
                    {
                        selectedTilesList.Add(tile);
                    }
                }
                else
                {
                    selectedTilesList.Add(tile);
                }
            }

            if (selectedTilesList.Count < 2) return;

            isTileSwapping = true;
            bool success = await SwapAndMatchAsync(selectedTilesList[0], selectedTilesList[1]);
            if (!success)
            {
                await SwapAsync(selectedTilesList[0], selectedTilesList[1]);
            }
            isTileSwapping = false;

            selectedTilesList.Clear();
            EnsurePlayableBoard();
        }

        // swap and match tiles asynchronously
        private async Task<bool> SwapAndMatchAsync(Tile tile1, Tile tile2)
        {
            await SwapAsync(tile1, tile2);

            if (await TryMatchAsync())
            {
                return true;
            }

            return false;
        }

        //  swap tiles asynchronously
        private async Task SwapAsync(Tile tile1, Tile tile2)
        {
            var icon1 = tile1.icon;
            var icon2 = tile2.icon;

            var icon1Transform = icon1.transform;
            var icon2Transform = icon2.transform;

            icon1Transform.SetParent(swapOverlayTransform);
            icon2Transform.SetParent(swapOverlayTransform);

            icon1Transform.SetAsLastSibling();
            icon2Transform.SetAsLastSibling();

            icon1Transform.SetParent(tile2.transform);
            icon2Transform.SetParent(tile1.transform);

            tile1.icon = icon2;
            tile2.icon = icon1;

            var tile1Item = tile1.Type;

            tile1.Type = tile2.Type;
            tile2.Type = tile1Item;
        }

        // ensure the board has playable moves
        private void EnsurePlayableBoard()
        {
            var matrix = Matrix;

            while (TileDataMatrixUtility.FindBestMove(matrix) == null || TileDataMatrixUtility.FindBestMatch(matrix) != null)
            {
                Shuffle();
                matrix = Matrix;
            }
        }

        // update the score
        public void ScoreUpdate()
        {
            playerScore += 30;
            scoreTextComponent.text = "Score: " + playerScore + "/700";
        }

        // try matching tiles asynchronously
        private async Task<bool> TryMatchAsync()
        {
            var didMatch = false;

            isTileMatching = true;

            var match = TileDataMatrixUtility.FindBestMatch(Matrix);

            while (match != null)
            {
                didMatch = true;

                var tiles = GetTiles(match.Tiles);

                var deflateSequence = DOTween.Sequence();

                foreach (var tile in tiles) deflateSequence.Join(tile.icon.transform.DOScale(Vector3.zero, tileTweenDuration).SetEase(Ease.InBack));

                audioSourceComponent.PlayOneShot(matchSoundClip);

                await deflateSequence.Play().AsyncWaitForCompletion();

                var inflateSequence = DOTween.Sequence();

                foreach (var tile in tiles)
                {
                    tile.Type = tileTypes[Random.Range(0, tileTypes.Length)];

                    inflateSequence.Join(tile.icon.transform.DOScale(Vector3.one, tileTweenDuration).SetEase(Ease.OutBack));
                }
                ScoreUpdate();

                await inflateSequence.Play().AsyncWaitForCompletion();

                OnMatch?.Invoke(Array.Find(tileTypes, tileType => tileType.id == match.TypeId), match.Tiles.Length);

                match = TileDataMatrixUtility.FindBestMatch(Matrix);
            }
            isTileMatching = false;

            return didMatch;
        }

        //shuffle the tiles on the board
        private void Shuffle()
        {
            isBoardShuffling = true;

            foreach (var row in rows)
                foreach (var tile in row.tiles)
                    tile.Type = tileTypes[Random.Range(0, tileTypes.Length)];

            isBoardShuffling = false;
        }

        public void ButtonActivated()
        {
            eventGameObject.SetActive(false);
            StartCoroutine("ResetEvent", 0.1f);
        }

        //Coroutine to reset the event game object
        public IEnumerator ResetEvent()
        {
            eventGameObject.SetActive(true);
            yield return new WaitForSeconds(0.1f);
        }
    }
}
