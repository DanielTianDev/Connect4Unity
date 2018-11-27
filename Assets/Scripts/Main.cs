using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.UI;
using System.IO;
using System;
//https://www.quora.com/How-do-I-make-Minimax-algorithm-incredibly-fast-How-do-I-deepen-the-game-search-tree
/**
 * @Author: Daniel Tian (A00736794)
 * @Date: November 26, 2018
 * 
 * Connect 4 Ai assignment 5 using minimax algorithm, alpha beta pruning, transposition tables of previously computed values from static evaluation, multi threading,
 * as well as loading in precomputed transposition table from a file to save computation time. 
 * This program can play with a depth of 9 within a reasonable amount of time (10 seconds average for the first 5-8 moves, and then gets much faster)
 * 
 * */
public class Main : MonoBehaviour {

    #region Declarations

    //Visual prefabs
    public GameObject connect4Prefab;
    public GameObject minimizerPrefab, maximizerPrefab;
    public GameObject[] animationPrefabs;
    public GameObject winningHighlightPrefab;
    public GameObject explosionPrefab;
    public GameObject cursorPrefab;

    //visual robot speed and max depth can be set here.
    public int MAX_DEPTH = 5;
    public float robotSpeed = 3.5f;
    public float currentTimer = 0f;
    public int evalCount = 0;
    public int hashHitCount = 0;

    //Board and game state data
    int[,] Board = new int[6, 7];
    readonly int MAXIMIZER = 1;
    readonly int MINIMIZER = 2;
    readonly int winScore = 10000000;
    readonly int loseScore = -10000000;
    readonly int boardRows = 6;
    readonly int boardColumns = 7;

    //Game state data
    int currentSelectionIndex = 0;
    int maximumDepthTravelled = 0;
    int aiColIndex = 0;
    int aiRow = 0;
    int playerScore, aiScore;

    //Game state booleans for visual effects and turn end
    bool aiTurn = false;
    bool threadFin;
    bool robotThinking;
    bool isGameOver;
    bool hasSelected;
    bool incrementTimer;
    readonly object minimaxLock = new object(); //used for multi threading

    //Visual element references
    GameObject[,] VisualBoard = new GameObject[6,7];
    GameObject[] PieceSpawnLocations = new GameObject[7];
    GameObject Robot;
    GameObject parentTransform;
    GameObject visualArrow;
    GameObject cursorPrefabPointer;
    GameObject selectionPanel;

    //Visual element references
    LinkedList<Vector3> winLocations = new LinkedList<Vector3>();
    LinkedList<GameObject> placedPieces = new LinkedList<GameObject>();
    LinkedList<GameObject> tempHighlight = new LinkedList<GameObject>();

    //Visual element references and controllers
    Animator robotAnimator;
    Text gameStatusText, scoreText, info1Text;
    TextMesh robotStatusText;
    Vector3 roboOriginalLocation;

    //Transposition Table
    Hashtable transpositionTable = new Hashtable();

    //Ai turn thread - computes minimax in a thread as to not freeze unity
    Thread _AiTurnThread, _MiniMaxThread;

    #endregion

    void Start() {
        parentTransform = GameObject.Find("Board");
        SpawnVisualBoard();  //Spawns connect 4 board into game world

        visualArrow = GameObject.Find("ArrowContainer");
        visualArrow.transform.position = new Vector3(PieceSpawnLocations[currentSelectionIndex].transform.position.x, PieceSpawnLocations[currentSelectionIndex].transform.position.y + 1.5f, PieceSpawnLocations[currentSelectionIndex].transform.position.z);

        Robot = GameObject.Find("Robot"); //spawn robot
        robotAnimator = Robot.GetComponent<Animator>();
        Robot.transform.position = new Vector3(PieceSpawnLocations[currentSelectionIndex].transform.position.x, PieceSpawnLocations[currentSelectionIndex].transform.position.y+0.5f, PieceSpawnLocations[currentSelectionIndex].transform.position.z);
        roboOriginalLocation = Robot.transform.position;

        robotStatusText = GameObject.Find("ThinkingText").GetComponent<TextMesh>(); 
        gameStatusText = GameObject.Find("StatusText").GetComponent<Text>();
        scoreText = GameObject.Find("ScoreText").GetComponent<Text>();
        info1Text = GameObject.Find("Info1Text").GetComponent<Text>();
        selectionPanel = GameObject.Find("Panel");
        cursorPrefabPointer = Instantiate(cursorPrefab, cursorPrefab.transform.position, cursorPrefab.transform.rotation);

        transpositionTable = LoadTranspositionTable();  //Loads in transposition table from file
    }

    #region UnityFunctions

    private void FixedUpdate()
    {
        if (incrementTimer) currentTimer += Time.fixedDeltaTime;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) SaveTranspositionTable(); //saves the hash table of values and board states

        if (threadFin) //Ai thread finished
        {
            threadFin = false;
            StartCoroutine(AnimateRobot());
            robotStatusText.text = "";
            info1Text.text = "Total Moves evaluated: " + (evalCount + hashHitCount) + "\n\nStatic Evaluation Calls: " + evalCount + "\n\nHashtable hits: " + hashHitCount +
                "\n\nMax depth traversed: " + maximumDepthTravelled + "\n\nTime taken: " + currentTimer + " seconds" + "\n\n\n\n\nTotal Hashtable Entries: " + transpositionTable.Count;
            maximumDepthTravelled = 0;
            evalCount = 0;
            hashHitCount = 0;
            currentTimer = 0;
        }

        if (Input.GetKeyDown(KeyCode.R)) ResetGame();
        if (isGameOver || !hasSelected) return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(1))
        {
            ExecuteTurn(MINIMIZER);
            ExecuteTurn(MAXIMIZER); //ai turn
        }

        if (Input.GetKeyDown(KeyCode.F10)) ExecuteTurn(MINIMIZER);//player debug
 
        if (Input.GetKeyDown(KeyCode.F11)) ExecuteTurn(MAXIMIZER);  //ai debug        

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            currentSelectionIndex--;
            if (currentSelectionIndex < 0) currentSelectionIndex = 6;
            visualArrow.transform.position = new Vector3(PieceSpawnLocations[currentSelectionIndex].transform.position.x, PieceSpawnLocations[currentSelectionIndex].transform.position.y + 1.5f, PieceSpawnLocations[currentSelectionIndex].transform.position.z-0.5f);
        }
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            currentSelectionIndex++;
            if (currentSelectionIndex >= 7) currentSelectionIndex = 0;
            visualArrow.transform.position = new Vector3(PieceSpawnLocations[currentSelectionIndex].transform.position.x, PieceSpawnLocations[currentSelectionIndex].transform.position.y + 1.5f, PieceSpawnLocations[currentSelectionIndex].transform.position.z - 0.5f);
        }
        UpdateVisualCursor();
    }

    void UpdateVisualCursor()
    {
        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out hit))
        {
            Transform objectHit = hit.transform;

            if(objectHit.tag == "piece")
            {
                currentSelectionIndex = (int)-objectHit.position.x;
                visualArrow.transform.position = new Vector3(PieceSpawnLocations[currentSelectionIndex].transform.position.x, PieceSpawnLocations[currentSelectionIndex].transform.position.y + 1.5f, PieceSpawnLocations[currentSelectionIndex].transform.position.z - 0.5f);
            }

            cursorPrefabPointer.transform.position = new Vector3(objectHit.transform.position.x, objectHit.transform.position.y+0.1f, objectHit.transform.position.z+0.5f);
        }
    }

    public void ChangeDifficulty()
    {
        MAX_DEPTH = (int)GameObject.Find("DifficultySlider").GetComponent<Slider>().value;
        GameObject.Find("DifficultyText").GetComponent<Text>().text = "Difficulty(depth): " + MAX_DEPTH;
    }

    void SpawnVisualBoard()
    {
        for(int row = 0; row < 6; row++)
        {
            for(int col = 0; col < 7; col++)
            {
                var go = Instantiate(connect4Prefab, new Vector3(-col,-row,0), connect4Prefab.transform.rotation);
                go.transform.parent = parentTransform.transform;
                go.name = "Location row: " + row + " col: " + col;
                VisualBoard[row, col] = go;
            }
        }
        for(int col = 0; col < 7; col++) PieceSpawnLocations[col] = VisualBoard[0, col];
    }
    
    public void ResetGame() //Resets the game
    {
        for(int row = 0; row < 6; row++)
            for(int col = 0; col < 7; col++) Board[row, col] = 0;

        foreach (var go in placedPieces) Destroy(go);
        foreach (var go in tempHighlight) Destroy(go);

        placedPieces.Clear();
        winLocations.Clear();

        Robot.transform.position = roboOriginalLocation;
        info1Text.text = "";
        robotAnimator.SetBool("dead", false);
        gameStatusText.text = "";
        isGameOver = false;
        selectionPanel.SetActive(true);
        hasSelected = false;
    }

    IEnumerator RobotDeath() //animates robot death
    {
        robotAnimator.SetBool("dead", true);
        yield return new WaitForSeconds(0.65f);
        Robot.transform.position = new Vector3(roboOriginalLocation.x, roboOriginalLocation.y-0.65f, roboOriginalLocation.z);
    }

    IEnumerator AnimateRobotThinkingText() //animates ai thinking
    {
        int temp = 0;
        robotStatusText.transform.position = new Vector3(Robot.transform.position.x+0.5f, Robot.transform.position.y + 2.25f, Robot.transform.position.z);
        while(robotThinking){
            if(temp == 0){
                robotStatusText.text = "Thinking";
                temp = 1;
            }else if(temp== 1){
                robotStatusText.text = "Thinking.";
                temp = 2;
            }else if(temp==2){
                robotStatusText.text = "Thinking..";
                temp = 3;
            }else{
                robotStatusText.text = "Thinking...";
                temp = 0;
            }
            yield return new WaitForSeconds(0.4f);
        }
        robotStatusText.text = "";
    }

    IEnumerator AnimateRobot() //animates ai placing a piece down at a specific column
    {
        Vector3 GoalPos = PieceSpawnLocations[aiColIndex].transform.position;
        GoalPos.y = Robot.transform.position.y;
        Robot.transform.LookAt(new Vector3(GoalPos.x, Robot.transform.position.y, GoalPos.z));
        robotAnimator.SetBool("isRun", true);

        while (Vector3.Distance(Robot.transform.position, GoalPos) >= 0.25)
        {
            Robot.transform.position = Vector3.MoveTowards(Robot.transform.position, GoalPos, robotSpeed * Time.fixedDeltaTime);
            yield return null;
        }

        Robot.transform.LookAt(new Vector3(Camera.main.transform.position.x, Robot.transform.position.y, Camera.main.transform.position.z));
        robotAnimator.SetBool("attack", true);

        int rand = UnityEngine.Random.Range(0, animationPrefabs.Length); //Picks a random particle effect to display when placing down piece
        Destroy(Instantiate(animationPrefabs[rand], new Vector3(PieceSpawnLocations[aiColIndex].transform.position.x, PieceSpawnLocations[aiColIndex].transform.position.y+.5f, PieceSpawnLocations[aiColIndex].transform.position.z), animationPrefabs[rand].transform.rotation), 2f);

        yield return new WaitForSeconds(0.75f); //teleports robot to the column where it's supposed to be placing a piece
        Robot.transform.position = new Vector3(PieceSpawnLocations[aiColIndex].transform.position.x, PieceSpawnLocations[aiColIndex].transform.position.y+.5f, PieceSpawnLocations[aiColIndex].transform.position.z);
        robotAnimator.SetBool("isRun", false);
        robotAnimator.SetBool("attack", false);

        var go = Instantiate(maximizerPrefab, PieceSpawnLocations[aiColIndex].transform.position, maximizerPrefab.transform.rotation); //spawns a visual piece in the world
        go.GetComponent<Piece>().SetCol(VisualBoard[aiRow, aiColIndex].transform.position);
        placedPieces.AddFirst(go);
        if (HasWon(MAXIMIZER))
        {
            isGameOver = true;
            foreach (var pos in winLocations) tempHighlight.AddFirst(Instantiate(winningHighlightPrefab, new Vector3(pos.x, pos.y, pos.z), winningHighlightPrefab.transform.rotation));
            Destroy(Instantiate(explosionPrefab), 10f);
            gameStatusText.text = "AI has won!";
            scoreText.text = "Player " + playerScore + " | " + " Ai: " + ++aiScore;
            //SaveTranspositionTable();
        }
        aiTurn = false;
    }

    #endregion

    #region MiniMax

    async Task<int> FindBestMove(int[,] board, int testParam) //multi threaded
    {
        int bestVal = int.MinValue;
        int bestMoveColumnIndex = 0;
        incrementTimer = true;

        int dynamicDepth = MAX_DEPTH;
        if(MAX_DEPTH > 5) //only make it difficult if player chose a hard difficulty
        {
            int columnsOccupied = 0;
            for (int col = 0; col < boardColumns; col++) if (board[0, col] != 0) columnsOccupied++;
            switch (columnsOccupied)
            {
                case 1:
                    dynamicDepth = 9;
                    break;
                case 2:
                    dynamicDepth = 11;
                    break;
                case 3:
                    dynamicDepth = 13;
                    break;
                case 4:
                    dynamicDepth = 19;
                    break;
                case 5:
                    dynamicDepth = 21;
                    break;
                case 6:
                    dynamicDepth = 23;
                    break;

            }
        }
        
        var tasks = new List<Task<int>>();
        var evaluatedColumns = new List<int>();

        for (int col = 0; col < 7; col++)
        {
            if (Board[0, col] == 0) //check if empty
            {
                int[,] tempBoard = CopyBoard(board);
                PlacePiece(col, tempBoard, MAXIMIZER);
                tasks.Add(Task.Run(() => Minimax(tempBoard, 0, dynamicDepth, false, int.MinValue, int.MaxValue)));
                evaluatedColumns.Add(col);
            }
        }

        await Task.WhenAll(tasks.ToArray());

        for (int c = 0; c < evaluatedColumns.Count; c++)
        {
            int val = tasks[c].Result;
            if (val > bestVal)
            {
                bestMoveColumnIndex = evaluatedColumns[c];
                bestVal = val;
            }
        }
        print("best move index: " + bestMoveColumnIndex);
        incrementTimer = false;
        return bestMoveColumnIndex;
    }

    int Minimax(int[,] board, int currentDepth, int maxDepth, bool isMax, int alpha, int beta)
    {
        if (maximumDepthTravelled < currentDepth) maximumDepthTravelled = currentDepth;
        int hash = GetBoardHash(board), score; //score = Evaluate(board, isMax) - currentDepth; 

        lock (minimaxLock)  //hashtable lookup for scores
        {
            if (!transpositionTable.Contains(hash))
            {
                score = Evaluate(board, isMax);
                transpositionTable.Add(hash, score);
            }
            else
            {
                hashHitCount++;
                score = (int)transpositionTable[hash];
            }
            score -= currentDepth;
        }

        if (score >= winScore - 100000) return score - currentDepth;
        if (score < loseScore + 100000) return score + currentDepth; //minimizer won
        if (!IsMovesLeft() || currentDepth >= maxDepth) return score - currentDepth; //currentDepth >= maxDepth

        if (isMax)
        {
            int best = int.MinValue;
            int val = 0;
            for (int col = 3; val <= 3; val++) //traverse all cells
            {
                if (val == 0)
                {
                    if (Board[0, col] == 0) //check if board empty
                    {
                        int[,] tempBoard = CopyBoard(board);  //make the move
                        PlacePiece(col, tempBoard, MAXIMIZER);
                        int result = Minimax(tempBoard, currentDepth + 1, maxDepth, !isMax, alpha, beta); //call minimax recursively and choose the max value
                        if (result > best) best = result;
                        if (best > alpha) alpha = best;
                        if (alpha >= beta) break; //if (beta <= alpha) break;
                    }
                }
                else
                {
                    if (Board[0, col - val] == 0) //check left
                    {
                        int[,] tempBoard = CopyBoard(board);  //make the move
                        PlacePiece(col - val, tempBoard, MAXIMIZER);
                        int result = Minimax(tempBoard, currentDepth + 1, maxDepth, !isMax, alpha, beta);
                        if (result > best) best = result;
                        if (best > alpha) alpha = best;
                        if (alpha >= beta) break;
                    }
                    if (Board[0, col + val] == 0) //check right
                    {
                        int[,] tempBoard = CopyBoard(board);  //make the move
                        PlacePiece(col + val, tempBoard, MAXIMIZER);
                        int result = Minimax(tempBoard, currentDepth + 1, maxDepth, !isMax, alpha, beta);
                        if (result > best) best = result;
                        if (best > alpha) alpha = best;
                        if (alpha >= beta) break;
                    }
                }
            }
            return best;
        }
        else // If this minimizer's move 
        {
            int best = int.MaxValue;
            int val = 0;
            for (int col = 3; val <= 3; val++)
            {
                if (val == 0)
                {
                    if (Board[0, col] == 0) //check if board empty
                    {
                        int[,] tempBoard = CopyBoard(board); //make the move
                        PlacePiece(col, tempBoard, MINIMIZER);
                        int result = Minimax(tempBoard, currentDepth + 1, maxDepth, !isMax, alpha, beta); //call minimax recursively and choose the mininum value
                        if (result < best) best = result;
                        if (best < beta) beta = best;
                        if (alpha >= beta) break; //if(beta <= alpha) break;
                    }
                }
                else
                {
                    if (Board[0, col - val] == 0) //check left
                    {
                        int[,] tempBoard = CopyBoard(board); //make the move
                        PlacePiece(col - val, tempBoard, MINIMIZER);
                        int result = Minimax(tempBoard, currentDepth + 1, maxDepth, !isMax, alpha, beta); //call minimax recursively and choose the mininum value
                        if (result < best) best = result;
                        if (best < beta) beta = best;
                        if (alpha >= beta) break;
                    }
                    if (Board[0, col + val] == 0) //check right
                    {
                        int[,] tempBoard = CopyBoard(board); //make the move
                        PlacePiece(col + val, tempBoard, MINIMIZER);
                        int result = Minimax(tempBoard, currentDepth + 1, maxDepth, !isMax, alpha, beta);
                        if (result < best) best = result;
                        if (best < beta) beta = best;
                        if (alpha >= beta) break;
                    }
                }
            }
            return best;
        }
    }

    public void SelectWhoGoesFirst(int selection)
    {
        if (selection == 0) ExecuteTurn(MAXIMIZER); //ai turn
        selectionPanel.SetActive(false);
        hasSelected = true;
    }

    void ExecuteTurn(int player)    //executes a turn for either the minimizer or maximizer
    {
        if (aiTurn || isGameOver) return;

        if (player == MAXIMIZER) //Ai Player as maximizer
        {
            robotThinking = true;
            StartCoroutine(AnimateRobotThinkingText());
            _AiTurnThread = new Thread(AiTurnThread);
            if (!_AiTurnThread.IsAlive) _AiTurnThread.Start();
        }
        else if (player == MINIMIZER)    //human player as minimizer
        {
            int r = PlacePiece(currentSelectionIndex, Board, MINIMIZER);
            if (r == -1) return;
            var go = Instantiate(minimizerPrefab, PieceSpawnLocations[currentSelectionIndex].transform.position, minimizerPrefab.transform.rotation);
            go.GetComponent<Piece>().SetCol(VisualBoard[r, currentSelectionIndex].transform.position);
            placedPieces.AddFirst(go);
            if (HasWon(MINIMIZER))
            {
                isGameOver = true;
                Destroy(Instantiate(explosionPrefab), 10f);
                foreach (var pos in winLocations) tempHighlight.AddFirst(Instantiate(winningHighlightPrefab, new Vector3(pos.x, pos.y, pos.z), winningHighlightPrefab.transform.rotation));
                StartCoroutine(RobotDeath());
                gameStatusText.text = "Player has won!";
                scoreText.text = "Player " + ++playerScore + " | " + " Ai: " + aiScore;
                return;
            }
        }
    }

    void AiTurnThread() //Ai Thread - we don't want the game to freeze while minimax is recursing
    {
        aiTurn = true;
        aiColIndex = FindBestMove(Board, 1337).Result;
        if (aiRow != -1) aiRow = PlacePiece(aiColIndex, Board, MAXIMIZER);
        threadFin = true;
        print("ai thread finished");
        robotThinking = false;
        Thread.CurrentThread.Abort(); //end current thread
    }

    #endregion

    #region EvaluationFunctions

    int Evaluate(int[,] board, bool isMax) //Scoring function - There should be 69 possible ways to win on an empty board.
    {
        evalCount++;
        for (int j = 0; j < boardColumns - 3; j++) // horizontalCheck
            for (int i = 0; i < boardRows; i++)
                if (board[i, j] == MAXIMIZER && board[i, j + 1] == MAXIMIZER && board[i, j + 2] == MAXIMIZER && board[i, j + 3] == MAXIMIZER) return winScore;
                else if (board[i, j] == MINIMIZER && board[i, j + 1] == MINIMIZER && board[i, j + 2] == MINIMIZER && board[i, j + 3] == MINIMIZER) return loseScore;

        for (int i = 0; i < boardRows - 3; i++) // verticalCheck
            for (int j = 0; j < boardColumns; j++)
                if (board[i, j] == MAXIMIZER && board[i + 1, j] == MAXIMIZER && board[i + 2, j] == MAXIMIZER && board[i + 3, j] == MAXIMIZER) return winScore;
                else if (board[i, j] == MINIMIZER && board[i + 1, j] == MINIMIZER && board[i + 2, j] == MINIMIZER && board[i + 3, j] == MINIMIZER) return loseScore;

        for (int i = 3; i < boardRows; i++)  // ascendingDiagonalCheck 
            for (int j = 0; j < boardColumns - 3; j++)
                if (board[i, j] == MAXIMIZER && board[i - 1, j + 1] == MAXIMIZER && board[i - 2, j + 2] == MAXIMIZER && board[i - 3, j + 3] == MAXIMIZER) return winScore;
                else if (board[i, j] == MINIMIZER && board[i - 1, j + 1] == MINIMIZER && board[i - 2, j + 2] == MINIMIZER && board[i - 3, j + 3] == MINIMIZER) return loseScore;

        for (int i = 3; i < boardRows; i++) // descendingDiagonalCheck
            for (int j = 3; j < boardColumns; j++)
                if (board[i, j] == MAXIMIZER && board[i - 1, j - 1] == MAXIMIZER && board[i - 2, j - 2] == MAXIMIZER && board[i - 3, j - 3] == MAXIMIZER) return winScore;
                else if (board[i, j] == MINIMIZER && board[i - 1, j - 1] == MINIMIZER && board[i - 2, j - 2] == MINIMIZER && board[i - 3, j - 3] == MINIMIZER) return loseScore;

        return HerrmannStaticEvaluation(board, isMax);
    }

    [Range(1, 300)]
    public int DefensiveFactor = 150;
    [Range(1, 300)]
    public int OffensiveFactor = 25;
    readonly int NumberToConnect = 4;
    readonly float maxAllowablePerc = 0.75f;

    int HerrmannStaticEvaluation(int[,] board, bool isMax)
    {
        int player = (isMax) ? MAXIMIZER: MINIMIZER;
        int maximizerTokens=0, minimizerTokens=0, emptyCount = 0;
        float danger = 0, goodness = 0;
        //1 - look at rows first (24 ways to win)
        for (int row = 0; row < board.GetLength(0); row++)
            for (int col = 0; col < 4; col++) //only need to look at 4 ways to win per 
            {
                if (board[row, col] == MAXIMIZER)
                    maximizerTokens++;
                else if (board[row, col] == MINIMIZER)
                    minimizerTokens++;
                else if (board[row, col] == 0)
                    emptyCount++;

                if (board[row, col+1] == MAXIMIZER)
                    maximizerTokens++;
                else if (board[row, col+1] == MINIMIZER)
                    minimizerTokens++;
                else if (board[row, col + 1] == 0)
                    emptyCount++;

                if (board[row, col+2] == MAXIMIZER)
                    maximizerTokens++;
                else if (board[row, col+2] == MINIMIZER)
                    minimizerTokens++;
                else if (board[row, col + 2] == 0)
                    emptyCount++;

                if (board[row, col + 3] == MAXIMIZER)
                    maximizerTokens++;
                else if (board[row, col + 3] == MINIMIZER)
                    minimizerTokens++;
                else if (board[row, col + 3] == 0)
                    emptyCount++;

                if (player == MINIMIZER)
                {
                    if(minimizerTokens == 0 && maximizerTokens >= 2) danger += (maximizerTokens / NumberToConnect) * (DefensiveFactor / maxAllowablePerc); //danger value
                   
                    if(maximizerTokens == 0) goodness += minimizerTokens * OffensiveFactor; //goodness values

                    if (minimizerTokens >= 1 && maximizerTokens >= 1)
                        if (emptyCount == 0) goodness += 12;
                        else goodness += 6;
                    
                }
                else if (player == MAXIMIZER)
                {
                    if (maximizerTokens == 0 && minimizerTokens >= 2) danger += (minimizerTokens / NumberToConnect) * (DefensiveFactor / maxAllowablePerc);
                    if (minimizerTokens == 0) goodness += maximizerTokens * OffensiveFactor; //goodness values
                    if (minimizerTokens >= 1 && maximizerTokens >= 1)
                        if (emptyCount == 0) goodness += 12;
                        else goodness += 6;
                }

                minimizerTokens = 0;
                maximizerTokens = 0;
                emptyCount = 0;

            }

        //2 - look at vertical (3 * 7 = 21 ways)
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < board.GetLength(1); col++) //only need to look at 4 ways to win per  
            {
                if (board[row, col] == MAXIMIZER)
                    maximizerTokens++;
                else if (board[row, col] == MINIMIZER)
                    minimizerTokens++;
                else if (board[row, col] == 0)
                    emptyCount++;

                if (board[row+1, col] == MAXIMIZER)
                    maximizerTokens++;
                else if (board[row+1, col] == MINIMIZER)
                    minimizerTokens++;
                else if (board[row+1, col] == 0)
                    emptyCount++;

                if (board[row+2, col] == MAXIMIZER)
                    maximizerTokens++;
                else if (board[row+2, col] == MINIMIZER)
                    minimizerTokens++;
                else if (board[row+2, col] == 0)
                    emptyCount++;

                if (board[row+3, col] == MAXIMIZER)
                    maximizerTokens++;
                else if (board[row+3, col] == MINIMIZER)
                    minimizerTokens++;
                else if (board[row+3, col] == 0)
                    emptyCount++;

                if (player == MINIMIZER)
                {
                    if (minimizerTokens == 0 && maximizerTokens >= 2) danger += (maximizerTokens / NumberToConnect) * (DefensiveFactor / maxAllowablePerc); //danger value

                    if (maximizerTokens == 0) goodness += minimizerTokens * OffensiveFactor; //goodness values

                    if (minimizerTokens >= 1 && maximizerTokens >= 1)
                        if (emptyCount == 0) goodness += 12;
                        else goodness += 6;
                }
                else if (player == MAXIMIZER)
                {
                    if (maximizerTokens == 0 && minimizerTokens >= 2) danger += (minimizerTokens / NumberToConnect) * (DefensiveFactor / maxAllowablePerc);  //otherplayertoken / number to connect
                    if (minimizerTokens == 0) goodness += maximizerTokens * OffensiveFactor; //goodness values
                    if (minimizerTokens >= 1 && maximizerTokens >= 1)
                        if (emptyCount == 0) goodness += 12;
                        else goodness += 6;
                }

                minimizerTokens = 0;
                maximizerTokens = 0;
                emptyCount = 0;

            }


        //3 - look at diagonal right and down (12), right to up (12)
        for (int col = 0; col < 4; col++)
        {
            for (int row = 0; row < 3; row++)
            {
                if (board[row, col] == MAXIMIZER)
                    maximizerTokens++;
                else if (board[row, col] == MINIMIZER)
                    minimizerTokens++;
                else if (board[row, col] == 0)
                    emptyCount++;

                if (board[row + 1, col+1] == MAXIMIZER)
                    maximizerTokens++;
                else if (board[row + 1, col + 1] == MINIMIZER)
                    minimizerTokens++;
                else if (board[row + 1, col + 1] == 0)
                    emptyCount++;

                if (board[row + 2, col + 2] == MAXIMIZER)
                    maximizerTokens++;
                else if (board[row + 2, col + 2] == MINIMIZER)
                    minimizerTokens++;
                else if (board[row + 2, col + 2] == 0)
                    emptyCount++;

                if (board[row + 3, col + 3] == MAXIMIZER)
                    maximizerTokens++;
                else if (board[row + 3, col + 3] == MINIMIZER)
                    minimizerTokens++;
                else if (board[row + 3, col + 3] == 0)
                    emptyCount++;

                if (player == MINIMIZER)
                {
                    if (minimizerTokens == 0 && maximizerTokens >= 2) danger += (maximizerTokens / NumberToConnect) * (DefensiveFactor / maxAllowablePerc); //danger value
                    if (maximizerTokens == 0) goodness += minimizerTokens * OffensiveFactor; //goodness values
                    if (minimizerTokens >= 1 && maximizerTokens >= 1)
                        if (emptyCount == 0) goodness += 12;
                        else goodness += 6;
                }
                else if (player == MAXIMIZER)
                {
                    if (maximizerTokens == 0 && minimizerTokens >= 2) danger += (minimizerTokens / NumberToConnect) * (DefensiveFactor / maxAllowablePerc);
                    if (minimizerTokens == 0) goodness += maximizerTokens * OffensiveFactor; //goodness values
                    if (minimizerTokens >= 1 && maximizerTokens >= 1)
                        if (emptyCount == 0) goodness += 12;
                        else goodness += 6;
                }
                minimizerTokens = 0;
                maximizerTokens = 0;
                emptyCount = 0;
            }

            for (int row = 3; row < 6; row++)
            {
                if (board[row, col] == MAXIMIZER)
                    maximizerTokens++;
                else if (board[row, col] == MINIMIZER)
                    minimizerTokens++;
                else if (board[row, col] == 0)
                    emptyCount++;

                if (board[row - 1, col + 1] == MAXIMIZER)
                    maximizerTokens++;
                else if (board[row - 1, col + 1] == MINIMIZER)
                    minimizerTokens++;
                else if (board[row - 1, col + 1] == 0)
                    emptyCount++;

                if (board[row - 2, col + 2] == MAXIMIZER)
                    maximizerTokens++;
                else if (board[row - 2, col + 2] == MINIMIZER)
                    minimizerTokens++;
                else if (board[row - 2, col + 2] == 0)
                    emptyCount++;

                if (board[row - 3, col + 3] == MAXIMIZER)
                    maximizerTokens++;
                else if (board[row - 3, col + 3] == MINIMIZER)
                    minimizerTokens++;
                else if (board[row - 3, col + 3] == 0)
                    emptyCount++;

                if (player == MINIMIZER)
                {
                    if (minimizerTokens == 0 && maximizerTokens >= 2) danger += (maximizerTokens / NumberToConnect) * (DefensiveFactor / maxAllowablePerc); //danger value
                    if (maximizerTokens == 0) goodness += minimizerTokens * OffensiveFactor; //goodness values
                    if (minimizerTokens >= 1 && maximizerTokens >= 1)
                        if (emptyCount == 0) goodness += 12;
                        else goodness += 6;
                }
                else if (player == MAXIMIZER)
                {
                    if (maximizerTokens == 0 && minimizerTokens >= 2) danger += (minimizerTokens / NumberToConnect) * (DefensiveFactor / maxAllowablePerc);
                    if (minimizerTokens == 0) goodness += maximizerTokens * OffensiveFactor; //goodness values
                    if (minimizerTokens >= 1 && maximizerTokens >= 1)
                        if (emptyCount == 0) goodness += 12;
                        else goodness += 6;
                }
                minimizerTokens = 0;
                maximizerTokens = 0;
                emptyCount = 0;
            }
        }
        return (player == MAXIMIZER) ? (int)(goodness - danger) : (int)(goodness - danger) * -1;  //if (player == MAXIMIZER) return (int)(goodness - danger); else return (int)(goodness - danger) * -1;
    }

    /**
     * A faster evaluation function - prioritizes moves nearer to the middle of the board, as they have higher value. 
     * Source = https://softwareengineering.stackexchange.com/questions/263514/why-does-this-evaluation-function-work-in-a-connect-four-game-in-java
     *  
     *  This function utilizes the values from evaluation table, and the numbers within evaluation table basically tells the computer
     *  the total number of possible 4 in a rows that are included in that spot. so The top left corner can only have 3 possible 4 in a rows (one horizontal, one vertical, one diagonal)
     *  The more possible 4 in a rows, the higher the score.
     *  
     *  The value in each square is a herustic of how useful any given square is for ultimately winning a game. It's not a perfect solution; rather it makes many assumptions in order to save performance.
     *  
     *  The utility value is 138, since the sum of all values in the table is 276 -> 276/2 = 138
     *  It returns 0 if both players are equally likely to win,
     *  a value smaller than 0 if minimizer is likely to win
     *  a value greater than 0 if maximizer is likely to win.
     * */
    readonly int[,] EvaluationTable =     {{3, 4, 5, 7, 5, 4, 3},
                                          {4, 6, 8, 10, 8, 6, 4},
                                          {5, 8, 11, 13, 11, 8, 5},
                                          {5, 8, 11, 13, 11, 8, 5},
                                          {4, 6, 8, 10, 8, 6, 4},
                                          {3, 4, 5, 7, 5, 4, 3}};
    int EvaluateContent(int[,] board)
    {
        int utility = 138, sum = 0;
        for (int i = 0; i < boardRows; i++)
            for (int j = 0; j < boardColumns; j++)
                if (board[i, j] == MAXIMIZER) sum += EvaluationTable[i, j];
                else if (board[i, j] == MINIMIZER) sum -= EvaluationTable[i, j];
        return utility + sum;
    }

    #endregion

    #region UtilityFunctions


    int[,] CopyBoard(int[,] board) //copys a board to another board, since passsing by value is hard
    {
        int[,] newArr = new int[6, 7];
        for (int row = 0; row < board.GetLength(0); row++)
            for (int col = 0; col < board.GetLength(1); col++) newArr[row, col] = board[row, col];

        return newArr;
    }

    int PlacePiece(int columnIndex, int[,] board, int player) //columnIndex should be between 0 and 6
    {
        if (columnIndex < 0 || columnIndex > board.GetLength(0)) return -1;

        if (board[0, columnIndex] != 0) return -1; //check if top spot is ok

        for (int row = board.GetLength(0) - 1; row >= 0; row--)
        {
            if (board[row, columnIndex] == 0)
            {
                board[row, columnIndex] = player;
                return row;
            }
        }
        return -1;
    }

    bool IsMovesLeft()
    {
        for (int row = 0; row < Board.GetLength(0); row++)
            for (int col = 0; col < Board.GetLength(1); col++)
                if (Board[row, col] == 0) return true;

        return false;
    }

    void PrintBoard(int[,] board)
    {
        string output = "";
        print("[");
        for (int row = 0; row < board.GetLength(0); row++)
        {
            for (int col = 0; col < board.GetLength(1); col++)
                output += board[row, col] + ", ";

            print(output);
            output = "";
        }
        print("]");
    }

    bool HasWon(int player)
    {
        for (int row = 0; row < Board.GetLength(0); row++)  //1 - look at rows first (24 ways to win)
            for (int col = 0; col < 4; col++) //only need to look at 4 ways to win per  
                if ((Board[row, col] != 0 && Board[row, col] == player) &&
                   (Board[row, col] == Board[row, col + 1]) &&
                   (Board[row, col] == Board[row, col + 2]) &&
                   (Board[row, col] == Board[row, col + 3]))
                {
                    winLocations.AddFirst(VisualBoard[row, col].transform.position);
                    winLocations.AddFirst(VisualBoard[row, col + 1].transform.position);
                    winLocations.AddFirst(VisualBoard[row, col + 2].transform.position);
                    winLocations.AddFirst(VisualBoard[row, col + 3].transform.position);
                    return true;
                }

        for (int row = 0; row < 3; row++)  //2 - look at vertical (3 * 7 = 21 ways)
            for (int col = 0; col < Board.GetLength(1); col++) //only need to look at 4 ways to win per  
                if ((Board[row, col] != 0 && Board[row, col] == player) &&
                   (Board[row, col] == Board[row + 1, col]) &&
                   (Board[row, col] == Board[row + 2, col]) &&
                   (Board[row, col] == Board[row + 3, col]))
                {
                    winLocations.AddFirst(VisualBoard[row, col].transform.position);
                    winLocations.AddFirst(VisualBoard[row + 1, col].transform.position);
                    winLocations.AddFirst(VisualBoard[row + 2, col].transform.position);
                    winLocations.AddFirst(VisualBoard[row + 3, col].transform.position);
                    return true;
                }

        for (int col = 0; col < 4; col++) //3 - look at diagonal right and down (12), right to up (12)
        {
            for (int row = 0; row < 3; row++)
                if ((Board[row, col] != 0 && Board[row, col] == player) &&
                    (Board[row, col] == Board[row + 1, col + 1]) &&
                    (Board[row, col] == Board[row + 2, col + 2]) &&
                    (Board[row, col] == Board[row + 3, col + 3]))
                {
                    winLocations.AddFirst(VisualBoard[row, col].transform.position);
                    winLocations.AddFirst(VisualBoard[row + 1, col + 1].transform.position);
                    winLocations.AddFirst(VisualBoard[row + 2, col + 2].transform.position);
                    winLocations.AddFirst(VisualBoard[row + 3, col + 3].transform.position);
                    return true;
                }

            for (int row = 3; row < 6; row++)
                if ((Board[row, col] != 0 && Board[row, col] == player) &&
                    (Board[row, col] == Board[row - 1, col + 1]) &&
                    (Board[row, col] == Board[row - 2, col + 2]) &&
                    (Board[row, col] == Board[row - 3, col + 3]))
                {
                    winLocations.AddFirst(VisualBoard[row, col].transform.position);
                    winLocations.AddFirst(VisualBoard[row - 1, col + 1].transform.position);
                    winLocations.AddFirst(VisualBoard[row - 2, col + 2].transform.position);
                    winLocations.AddFirst(VisualBoard[row - 3, col + 3].transform.position);
                    return true;
                }
        }
        return false;
    }


    #endregion

    #region TableLookupFunctions

    bool SaveTranspositionTable()
    {
        try
        {
            var binformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter(); // write the data to a file
            string dir = Directory.GetCurrentDirectory() + "\\TranspositionTable.txt";
            using (var fs = File.Create(dir))
            {
                binformatter.Serialize(fs, transpositionTable);
            }
        }
        catch (Exception e)
        {
            if (e.Message != null) print("Error saving file: " + e.Message);
            return false;
        }
        return true;
    }

    Hashtable LoadTranspositionTable()
    {
        Hashtable deserialized = null;
        try
        {
            string dir = Directory.GetCurrentDirectory() + "\\TranspositionTable.txt";
            var binformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            using (var fs = File.Open(dir, FileMode.Open))
            {
                deserialized = (Hashtable)binformatter.Deserialize(fs);
            }
        }
        catch (Exception e)
        {
            if (e.Message != null) print("Error reading data file: " + e.Message);
            return new Hashtable();
        }

        print("transpositionTable count: " + deserialized.Count);
        return deserialized;
    }

    int GetBoardHash(int[,] board)
    {
        unchecked
        {
            const int p = 16777619;
            int hash = (int)2166136261;
            for (int i = 0; i < board.GetLength(0); ++i)
                for (int j = 0; j < board.GetLength(1); ++j)
                {
                    hash = (hash ^ board[i, j]) * p;
                }

            hash += hash << 13;
            hash ^= hash >> 7;
            hash += hash << 3;
            hash ^= hash >> 17;
            hash += hash << 5;

            return hash;
        }
    }

    int GetNodeHash(int boardHash, int currentDepth, int alpha, int beta)
    {
        unchecked
        {
            const int p = 16777619;
            int hash = (int)2166136261;

            hash = (hash ^ boardHash) * p;
            hash = (hash ^ currentDepth) * p;
            hash = (hash ^ alpha) * p;
            hash = (hash ^ beta) * p;

            hash += hash << 13;
            hash ^= hash >> 7;
            hash += hash << 3;
            hash ^= hash >> 17;
            hash += hash << 5;

            return hash;
        }
    }
    #endregion

}
