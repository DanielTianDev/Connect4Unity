using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using UnityEngine.UI;
//https://www.quora.com/How-do-I-make-Minimax-algorithm-incredibly-fast-How-do-I-deepen-the-game-search-tree
/**
 * @Author: Daniel Tian
 * @Date: November 22, 2018
 * 
 * Connect 4 Ai assignment 5 using minimax algorithm, and an evaluation function based on the Gaussian normal distributrion of the table, giving higher scores
 * to positions nearer to the center of the board. There is improvements to be made, but it works alright as giving bias to the center positions will beat most humans
 * who aren't experts or too familiar with the game, provided we can look ahead far enough.
 * 
 * 
 * */
public class Main : MonoBehaviour {


    GameObject[,] VisualBoard = new GameObject[6,7];
    GameObject[] PieceSpawnLocations = new GameObject[7];

    public GameObject connect4Prefab;
    public GameObject minimizerPrefab, maximizerPrefab;
    public GameObject Robot;
    public Animator robotAnimator;
    public float robotSpeed = 1f;

    public GameObject[] animationPrefabs;
    public GameObject winningHighlightPrefab;
    public GameObject explosionPrefab;
    public GameObject cursorPrefab;

    public float aiTurnDelay = 0.55f;
    GameObject cursorPrefabPointer;

    GameObject parentTransform;
    GameObject visualArrow;
    int currentSelectionIndex = 0;

    int[,] Board = new int[6, 7];
    const int MAXIMIZER = 1;
    const int MINIMIZER = 2;
    public int MaxDepth = 5;

    readonly int winScore = 1000000;
    readonly int loseScore = -1000000;
    readonly int boardRows = 6;
    readonly int boardColumns = 7;

    bool aiTurn = false;

    Thread _t1;
    int aiColIndex;
    int aiRow;
    bool threadFin;
    LinkedList<GameObject> placedPieces = new LinkedList<GameObject>();

    Vector3 roboOriginalLocation;

    TextMesh robotStatusText;
    bool robotThinking;
    bool isGameOver;

    Text gameStatusText, scoreText;
    LinkedList<Vector3> winLocations = new LinkedList<Vector3>();
    LinkedList<GameObject> tempHighlight = new LinkedList<GameObject>();

    GameObject selectionPanel;

    int playerScore, aiScore;
    bool hasSelected;

    void Start() {
        parentTransform = GameObject.Find("Board");
        SpawnBoardLocations();

        visualArrow = GameObject.Find("ArrowContainer");
        visualArrow.transform.position = new Vector3(PieceSpawnLocations[currentSelectionIndex].transform.position.x, PieceSpawnLocations[currentSelectionIndex].transform.position.y + 1.5f, PieceSpawnLocations[currentSelectionIndex].transform.position.z);

        Robot = GameObject.Find("Robot");
        robotAnimator = Robot.GetComponent<Animator>();
        //spawn robot
        Robot.transform.position = new Vector3(PieceSpawnLocations[currentSelectionIndex].transform.position.x, PieceSpawnLocations[currentSelectionIndex].transform.position.y+0.5f, PieceSpawnLocations[currentSelectionIndex].transform.position.z);
        roboOriginalLocation = Robot.transform.position;

        robotStatusText = GameObject.Find("ThinkingText").GetComponent<TextMesh>(); 
        gameStatusText = GameObject.Find("StatusText").GetComponent<Text>();
        scoreText = GameObject.Find("ScoreText").GetComponent<Text>();
        selectionPanel = GameObject.Find("Panel");

        cursorPrefabPointer = Instantiate(cursorPrefab, cursorPrefab.transform.position, cursorPrefab.transform.rotation);

    }


    private void Update()
    {
        if (threadFin)
        {
            threadFin = false;
            StartCoroutine(AnimateRobot());
            robotStatusText.text = "";
        }

        if (Input.GetKeyDown(KeyCode.R)) ResetGame();

        if (isGameOver || !hasSelected) return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(1))
        {
            PlacePiece(MINIMIZER);
            PlacePiece(MAXIMIZER); //ai turn
        }

        if (Input.GetKeyDown(KeyCode.F10)) PlacePiece(MINIMIZER);//player debug
        if (Input.GetKeyDown(KeyCode.F11)) PlacePiece(MAXIMIZER);  //ai debug        

        if(Input.GetKeyDown(KeyCode.F12)) StartCoroutine( PlayTillEnd(aiTurnDelay));
       

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

        RayCastSelect();
    }

    void RayCastSelect()
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

            cursorPrefabPointer.transform.position = new Vector3(objectHit.transform.position.x, objectHit.transform.position.y, objectHit.transform.position.z+0.5f);
        }
    }

    public void ChangeDifficulty()
    {
        int val = (int)GameObject.Find("DifficultySlider").GetComponent<Slider>().value;
        MaxDepth = val;
        GameObject.Find("DifficultyText").GetComponent<Text>().text = "Difficulty(Depth): " + MaxDepth;
    }

    void SpawnBoardLocations()
    {
        for(int row = 0; row < 6; row++)
        {
            for(int col = 0; col < 7; col++)
            {
                var go = Instantiate(connect4Prefab, new Vector3(-col,-row,0), connect4Prefab.transform.rotation);
                //cube.transform.position = new Vector3(-col,-row,0);
                go.transform.parent = parentTransform.transform;
                go.name = "Location row: " + row + " col: " + col;
                VisualBoard[row, col] = go;
            }
        }

        for(int col = 0; col < 7; col++) PieceSpawnLocations[col] = VisualBoard[0, col];
        
    }


    public void SelectWhoGoesFirst(int selection)
    {
        if(selection == 0)
        {
            PlacePiece(MAXIMIZER); //ai turn
        }

        selectionPanel.SetActive(false);
        hasSelected = true;
    }

    void PlacePiece(int player)
    {
        if (aiTurn) return;

        if(player == MAXIMIZER) //Ai Player as maximizer
        {
            robotThinking = true;
            StartCoroutine(AnimateRobotThinkingText());

            _t1 = new Thread(_PlaceAiPiece);
            if (!_t1.IsAlive) _t1.Start();
        }
        else if(player == MINIMIZER)    //human player as minimizer
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
                foreach(var pos in winLocations) tempHighlight.AddFirst(Instantiate(winningHighlightPrefab, new Vector3(pos.x , pos.y, pos.z), winningHighlightPrefab.transform.rotation));
                StartCoroutine(RobotDeath());
                gameStatusText.text = "Player has won!";
                scoreText.text = "Player " + ++playerScore + " | " +  " Ai: " + aiScore;
                return;
            }
        }
    }

    private void _PlaceAiPiece() //Ai Thread - we don't want the game to lag while minimax is recursing
    {
        aiTurn = true;
        aiColIndex = FindBestMove(Board, MaxDepth);
        aiRow = PlacePiece(aiColIndex, Board, MAXIMIZER);

        threadFin = true;
        robotThinking = false;
        Thread.CurrentThread.Abort(); //end current thread
    }

    public void ResetGame()
    {
        for(int row = 0; row < 6; row++)
            for(int col = 0; col < 7; col++) Board[row, col] = 0;

        foreach (var go in placedPieces) Destroy(go);
        foreach (var go in tempHighlight) Destroy(go);
        placedPieces.Clear();
        winLocations.Clear();

        Robot.transform.position = roboOriginalLocation;
        robotAnimator.SetBool("dead", false);
        gameStatusText.text = "";
        isGameOver = false;

        selectionPanel.SetActive(true);
        hasSelected = false;
    }

    IEnumerator RobotDeath()
    {
        robotAnimator.SetBool("dead", true);
        yield return new WaitForSeconds(0.65f);
        Robot.transform.position = new Vector3(roboOriginalLocation.x, roboOriginalLocation.y-0.65f, roboOriginalLocation.z);
    }

    IEnumerator AnimateRobotThinkingText()
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

    IEnumerator AnimateRobot()
    {
        Vector3 GoalPos = PieceSpawnLocations[aiColIndex].transform.position;
        GoalPos.y = Robot.transform.position.y;
        Robot.transform.LookAt(new Vector3(GoalPos.x, Robot.transform.position.y, GoalPos.z));
        robotAnimator.SetBool("isRun", true);

        while (Vector3.Distance(Robot.transform.position, GoalPos) >= 0.25)
        {
            Robot.transform.position = Vector3.MoveTowards(Robot.transform.position, GoalPos, robotSpeed * Time.deltaTime);

            yield return null;
        }
        Robot.transform.LookAt(new Vector3(Camera.main.transform.position.x, Robot.transform.position.y, Camera.main.transform.position.z));
        robotAnimator.SetBool("attack", true);

        int rand = Random.Range(0, animationPrefabs.Length);
        Destroy(Instantiate(animationPrefabs[rand], new Vector3(PieceSpawnLocations[aiColIndex].transform.position.x, PieceSpawnLocations[aiColIndex].transform.position.y+.5f, PieceSpawnLocations[aiColIndex].transform.position.z), animationPrefabs[rand].transform.rotation), 2f);

        yield return new WaitForSeconds(0.75f);
        Robot.transform.position = new Vector3(PieceSpawnLocations[aiColIndex].transform.position.x, PieceSpawnLocations[aiColIndex].transform.position.y+.5f, PieceSpawnLocations[aiColIndex].transform.position.z);
        robotAnimator.SetBool("isRun", false);
        robotAnimator.SetBool("attack", false);

        var go = Instantiate(maximizerPrefab, PieceSpawnLocations[aiColIndex].transform.position, maximizerPrefab.transform.rotation);
        go.GetComponent<Piece>().SetCol(VisualBoard[aiRow, aiColIndex].transform.position);
        placedPieces.AddFirst(go);
        if (HasWon(MAXIMIZER))
        {
            isGameOver = true;
            foreach (var pos in winLocations) tempHighlight.AddFirst(Instantiate(winningHighlightPrefab, new Vector3(pos.x, pos.y, pos.z), winningHighlightPrefab.transform.rotation));
            Destroy(Instantiate(explosionPrefab), 10f);
            gameStatusText.text = "AI has won!";
            scoreText.text = "Player " + playerScore + " | " + " Ai: " + ++aiScore;
        }
        aiTurn = false;
    }

    IEnumerator PlayTillEnd(float delay)
    {
        while(!HasWon(MAXIMIZER) && !HasWon(MINIMIZER))
        {
            int colIndex = FindBestMove2(Board, MaxDepth);
            int r = PlacePiece(colIndex, Board, MINIMIZER);
            var go = Instantiate(minimizerPrefab, PieceSpawnLocations[colIndex].transform.position, minimizerPrefab.transform.rotation);
            go.GetComponent<Piece>().SetCol(VisualBoard[r, colIndex].transform.position);
            placedPieces.AddFirst(go);

            if (HasWon(MINIMIZER))
            {
                isGameOver = true;
                Destroy(Instantiate(explosionPrefab), 10f);
                foreach (var pos in winLocations) tempHighlight.AddFirst(Instantiate(winningHighlightPrefab, new Vector3(pos.x, pos.y, pos.z), winningHighlightPrefab.transform.rotation));
                StartCoroutine(RobotDeath());
                gameStatusText.text = "minimizer(1) has won!";
                scoreText.text = "Player " + ++playerScore + " | " + " Ai: " + aiScore;
                break;
            }

            yield return new WaitForSeconds(delay);

            colIndex = FindBestMove(Board, MaxDepth);
            r = PlacePiece(colIndex, Board, MAXIMIZER);
            var go2 = Instantiate(maximizerPrefab, PieceSpawnLocations[colIndex].transform.position, maximizerPrefab.transform.rotation);
            go2.GetComponent<Piece>().SetCol(VisualBoard[r, colIndex].transform.position);
            placedPieces.AddFirst(go2);

            if (HasWon(MAXIMIZER))
            {
                isGameOver = true;
                Destroy(Instantiate(explosionPrefab), 10f);
                foreach (var pos in winLocations) tempHighlight.AddFirst(Instantiate(winningHighlightPrefab, new Vector3(pos.x, pos.y, pos.z), winningHighlightPrefab.transform.rotation));
                StartCoroutine(RobotDeath());
                gameStatusText.text = "Maximizer(p2) has won!";
                scoreText.text = "Player " + ++playerScore + " | " + " Ai: " + aiScore;
                break;
            }
            yield return new WaitForSeconds(delay);
        }
    }

    int FindBestMove(int[,] board, int maxDepth)
    {
        int bestVal = int.MinValue;
        int bestMoveColumnIndex = 0;
        for (int col = 0; col < board.GetLength(1); col++)
        {
            if (Board[0, col] == 0) //check if board empty
            {
                //make the move
                int[,] tempBoard = CopyBoard(board);
                PlacePiece(col, tempBoard, MAXIMIZER);
                int moveVal = Minimax(tempBoard, maxDepth, 0, false, int.MinValue, int.MaxValue);
                if (moveVal > bestVal)
                {
                    bestMoveColumnIndex = col;
                    bestVal = moveVal;
                }
                print("col: " + col + " value: " + moveVal);
            }
        }
        print("best move index: " + bestMoveColumnIndex);
        return bestMoveColumnIndex;
    }

    int FindBestMove2(int[,] board, int maxDepth)
    {
        int bestVal = int.MaxValue;
        int bestMoveColumnIndex = 0;
        for (int col = 0; col < board.GetLength(1); col++)
        {
            if (Board[0, col] == 0) //check if board empty
            {
                //make the move
                int[,] tempBoard = CopyBoard(board);
                PlacePiece(col, tempBoard, MINIMIZER);
                int moveVal = Minimax(tempBoard, maxDepth, 0, true, int.MinValue, int.MaxValue);
                if (moveVal < bestVal)
                {
                    bestMoveColumnIndex = col;
                    bestVal = moveVal;
                }
            }
        }
        return bestMoveColumnIndex;
    }

    int Minimax(int[,] board, int maxDepth, int currentDepth, bool isMax, int alpha, int beta)
    {
        int score = Evaluate(board, isMax) - currentDepth;
        if (score >= winScore - 25000) return score - currentDepth;
        if (score < loseScore + 25000) return score + currentDepth; //minimizer won
        if (!IsMovesLeft() || currentDepth >= maxDepth) return score - currentDepth; //might be a tie? 

        if (isMax)
        {
            int best = int.MinValue;
            //traverse all cells
            for (int col = 0; col < Board.GetLength(1); col++)
                if (Board[0, col] == 0) //check if empty
                { 
                    int[,] tempBoard = CopyBoard(board);  //make the move
                    PlacePiece(col, tempBoard, MAXIMIZER);
                    //call minimax recursively and choose the max value
                    int result = Minimax(tempBoard, maxDepth, currentDepth + 1, !isMax, alpha, beta);
                    if (result > best) best = result;
                    if(best > alpha) alpha = best;
                    //if (beta <= alpha) break;
                    if (alpha >= beta) break;
                }
            return best;
        }
        else// If this minimizer's move 
        {
            int best = int.MaxValue;
            //traverse all cells
            for (int col = 0; col < Board.GetLength(1); col++)
                if (Board[0, col] == 0) //check if empty
                {
                    int[,] tempBoard = CopyBoard(board); //make the move
                    PlacePiece(col, tempBoard, MINIMIZER);
                    int result = Minimax(tempBoard, maxDepth, currentDepth + 1, !isMax, alpha, beta); //call minimax recursively and choose the mininum value
                    if (result < best) best = result;
                    if(best < beta) beta = best;
                    if (alpha >= beta) break;//if(beta <= alpha) break;
                }
            return best;
        }
    }

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


    #region EvaluationFunction

    readonly int[,] EvaluationTable =     {{3, 4, 5, 7, 5, 4, 3},
                                          {4, 6, 8, 10, 8, 6, 4},
                                          {5, 8, 11, 13, 11, 8, 5},
                                          {5, 8, 11, 13, 11, 8, 5},
                                          {4, 6, 8, 10, 8, 6, 4},
                                          {3, 4, 5, 7, 5, 4, 3}};
    /**
     * A better evaluation function - prioritizes moves nearer to the middle of the board, as they have higher value.
     *  
     *  This function utilizes the values from evaluation table above, and the numbers within evaluation table basically tells the computer
     *  the total number of possible 4 in a rows that are included in that spot. so The top left corner can only have 3 possible 4 in a rows (one horizontal, one vertical, one diagonal)
     *  The more possible 4 in a rows, the higher the score.
     *  
     *  The value in each square is a herustic of how useful any given square is for ultimately winning a game. It's not a perfect solution; rather it makes many assumptions in order to save performance.
     *  
     *  The utility value is 138, since t he sum of all values in the table is 276 -> 276/2 = 138
     *  It returns 0 if both players are equally likely to win,
     *  a value smaller than 0 if minimizer is likely to win
     *  a value greater than 0 if maximizer is likely to win.
     * */
    int EvaluateContent(int[,] board)
    {
        int utility = 138;
        int sum = 0;
        for (int i = 0; i < boardRows; i++)
            for (int j = 0; j < boardColumns; j++)
                if (board[i,j] == MAXIMIZER)
                    sum += EvaluationTable[i,j];
                else if (board[i,j] == MINIMIZER)
                    sum -= EvaluationTable[i,j];
        return utility + sum;
    }

    int Evaluate(int[,] board, bool isMax) //Scoring function - There should be 69 possible ways to win on an empty board.
    {

        // horizontalCheck 
        for (int j = 0; j < boardColumns - 3; j++)
            for (int i = 0; i < boardRows; i++)
                if (board[i, j] == MAXIMIZER && board[i, j + 1] == MAXIMIZER && board[i, j + 2] == MAXIMIZER && board[i, j + 3] == MAXIMIZER) return winScore;
                else if (board[i, j] == MINIMIZER && board[i, j + 1] == MINIMIZER && board[i, j + 2] == MINIMIZER && board[i, j + 3] == MINIMIZER) return loseScore;

        // verticalCheck
        for (int i = 0; i < boardRows - 3; i++)
            for (int j = 0; j < boardColumns; j++)
                if (board[i, j] == MAXIMIZER && board[i + 1, j] == MAXIMIZER && board[i + 2, j] == MAXIMIZER && board[i + 3, j] == MAXIMIZER) return winScore;
                else if (board[i, j] == MINIMIZER && board[i + 1, j] == MINIMIZER && board[i + 2, j] == MINIMIZER && board[i + 3, j] == MINIMIZER) return loseScore;

        // ascendingDiagonalCheck 
        for (int i = 3; i < boardRows; i++)
            for (int j = 0; j < boardColumns - 3; j++)
                if (board[i, j] == MAXIMIZER && board[i - 1, j + 1] == MAXIMIZER && board[i - 2, j + 2] == MAXIMIZER && board[i - 3, j + 3] == MAXIMIZER) return winScore;
                else if (board[i, j] == MINIMIZER && board[i - 1, j + 1] == MINIMIZER && board[i - 2, j + 2] == MINIMIZER && board[i - 3, j + 3] == MINIMIZER) return loseScore;

        // descendingDiagonalCheck
        for (int i = 3; i < boardRows; i++)
            for (int j = 3; j < boardColumns; j++)
                if (board[i, j] == MAXIMIZER && board[i - 1, j - 1] == MAXIMIZER && board[i - 2, j - 2] == MAXIMIZER && board[i - 3, j - 3] == MAXIMIZER) return winScore;
                else if (board[i, j] == MINIMIZER && board[i - 1, j - 1] == MINIMIZER && board[i - 2, j - 2] == MINIMIZER && board[i - 3, j - 3] == MINIMIZER) return loseScore;

        if(isMax)
            return EvaluateContent(board);
        else
            return GettingWinningMoveCount(board, MAXIMIZER) - GettingWinningMoveCount(board, MINIMIZER);
        //return EvaluateContent(board); //return GettingWinningMoveCount(board, MAXIMIZER) - GettingWinningMoveCount(board, MINIMIZER); this also works, but is less efficient
    }

    #endregion


    void PrintBoard(int[,] board)
    {
        string output = "";
        print("[");
        for (int row = 0; row < board.GetLength(0); row++) { 
            for (int col = 0; col < board.GetLength(1); col++)
                output += board[row, col] + ", ";
            
            print(output);
            output = "";
        }
        print("]");
    }


    const int threeInARowValue = 50;
    const int twoInARow = 10;
    //Test evaluation function. it works but is not efficient, since it needs to be called twice to subtract the score from maximizer - minimizer.
    int GettingWinningMoveCount(int[,] board, int player)
    {
        //There should be 69 possible ways to win on an empty board.
        int winningMoves = 0;
        //1 - look at rows first (24 ways to win)
        for (int row = 0; row < board.GetLength(0); row++)
            for (int col = 0; col < 4; col++) //only need to look at 4 ways to win per  
                if ((board[row, col] == 0 || board[row, col] == player) &&
                   (board[row, col + 1] == 0 || board[row, col + 1] == player) &&
                   (board[row, col + 2] == 0 || board[row, col + 2] == player) &&
                   (board[row, col + 3] == 0 || board[row, col + 3] == player))
                    winningMoves++;

        //2 - look at vertical (3 * 7 = 21 ways)
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < board.GetLength(1); col++) //only need to look at 4 ways to win per  
                if ((board[row, col] == 0 || board[row, col] == player) &&
                   (board[row + 1, col] == 0 || board[row + 1, col] == player) &&
                   (board[row + 2, col] == 0 || board[row + 2, col] == player) &&
                   (board[row + 3, col] == 0 || board[row + 3, col] == player))
                    winningMoves++;

        //3 - look at diagonal right and down (12), right to up (12)
        for (int col = 0; col < 4; col++)
        {
            for (int row = 0; row < 3; row++)
                if ((board[row, col] == 0 || board[row, col] == player) &&
                    (board[row + 1, col + 1] == 0 || board[row + 1, col + 1] == player) &&
                    (board[row + 2, col + 2] == 0 || board[row + 2, col + 2] == player) &&
                    (board[row + 3, col + 3] == 0 || board[row + 3, col + 3] == player))
                    winningMoves++;

            for (int row = 3; row < 6; row++)
            
                if ((board[row, col] == 0 || board[row, col] == player) &&
                    (board[row - 1, col + 1] == 0 || board[row - 1, col + 1] == player) &&
                    (board[row - 2, col + 2] == 0 || board[row - 2, col + 2] == player) &&
                    (board[row - 3, col + 3] == 0 || board[row - 3, col + 3] == player))
                    winningMoves++;
        }

        //Look at rows of XXX_ XX_X X_XX _XXX
        for (int row = 0; row < board.GetLength(0); row++)
            for (int col = 0; col < 4; col++) //only need to look at 4 ways to win per  
                if ((board[row, col] == player) &&
                   (board[row, col + 1] == player) &&
                   (board[row, col + 2] == player) &&
                   (board[row, col + 3] == 0))
                    winningMoves+=threeInARowValue;
                else if ((board[row, col] == player) &&
                   (board[row, col + 1] == player) &&
                   (board[row, col + 2] == 0) &&
                   (board[row, col + 3] == player))
                    winningMoves+=threeInARowValue;
                else if ((board[row, col] == player) &&
                   (board[row, col + 1] == 0) &&
                   (board[row, col + 2] == player) &&
                   (board[row, col + 3] == player))
                    winningMoves+=threeInARowValue;
                else if ((board[row, col] == 0) &&
                   (board[row, col + 1] == player) &&
                   (board[row, col + 2] == player) &&
                   (board[row, col + 3] == player))
                    winningMoves+=threeInARowValue;
                else if ((board[row, col] == player) && //look at 2 in a rows XX__ _XX_ __XX
                   (board[row, col + 1] == player) &&
                   (board[row, col + 2] == 0) &&
                   (board[row, col + 3] == 0))
                    winningMoves+=twoInARow;
                else if ((board[row, col] == 0) &&
                   (board[row, col + 1] == player) &&
                   (board[row, col + 2] == player) &&
                   (board[row, col + 3] == 0))
                    winningMoves+=twoInARow;
                else if ((board[row, col] == 0) &&
                   (board[row, col + 1] == 0) &&
                   (board[row, col + 2] == player) &&
                   (board[row, col + 3] == player))
                    winningMoves+=twoInARow;

        //Look at columns
        //_
        //_  
        //X 
        //X 
        for (int row = 5; row >= 3; row--)
            for (int col = 0; col < board.GetLength(1); col++)   
                if ((board[row, col] == player) &&
                   (board[row - 1, col] == player) &&
                   (board[row - 2, col] == 0) &&
                   (board[row - 3, col] == 0))
                    winningMoves += twoInARow;
                else if ((board[row, col] == player) &&
                          (board[row - 1, col] == player) &&
                          (board[row - 2, col] == player) &&
                          (board[row - 3, col] == 0))
                    winningMoves += threeInARowValue;

        return winningMoves;
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
                    winLocations.AddFirst(VisualBoard[row,col].transform.position);
                    winLocations.AddFirst(VisualBoard[row, col+1].transform.position);
                    winLocations.AddFirst(VisualBoard[row, col+2].transform.position);
                    winLocations.AddFirst(VisualBoard[row, col+3].transform.position);
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
                    winLocations.AddFirst(VisualBoard[row+1, col].transform.position);
                    winLocations.AddFirst(VisualBoard[row+2, col].transform.position);
                    winLocations.AddFirst(VisualBoard[row+3, col].transform.position);
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
                    winLocations.AddFirst(VisualBoard[row+1, col+1].transform.position);
                    winLocations.AddFirst(VisualBoard[row+2, col+2].transform.position);
                    winLocations.AddFirst(VisualBoard[row+3, col+3].transform.position);
                    return true;
                }

            for (int row = 3; row < 6; row++)
                if ((Board[row, col] != 0 && Board[row, col] == player) &&
                    (Board[row, col] == Board[row - 1, col + 1]) &&
                    (Board[row, col] == Board[row - 2, col + 2]) &&
                    (Board[row, col] == Board[row - 3, col + 3]))
                {
                    winLocations.AddFirst(VisualBoard[row, col].transform.position);
                    winLocations.AddFirst(VisualBoard[row-1, col+1].transform.position);
                    winLocations.AddFirst(VisualBoard[row-2, col+2].transform.position);
                    winLocations.AddFirst(VisualBoard[row-3, col+3].transform.position);
                    return true;
                }
        }
        return false;
    }

}
