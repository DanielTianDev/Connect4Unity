using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using UnityEngine.UI;
using UnityEngine.Animations;
//https://www.quora.com/How-do-I-make-Minimax-algorithm-incredibly-fast-How-do-I-deepen-the-game-search-tree
public class Main : MonoBehaviour {


    GameObject[,] VisualBoard = new GameObject[6,7];
    Vector3[] PieceSpawnLocations = new Vector3[7];

    public GameObject connect4Prefab;
    public GameObject minimizerPrefab, maximizerPrefab;
    public GameObject Robot;
    public Animator robotAnimator;
    public float robotSpeed = 1f;

    public GameObject[] animationPrefabs;
    public GameObject explosionPrefab;

    GameObject parentTransform;
    GameObject visualArrow;
    int currentSelectionIndex = 0;

    int[,] Board = new int[6, 7];
    const int MAXIMIZER = 1;
    const int MINIMIZER = 2;
    public int MaxDepth = 5;

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

    Text gameStatusText;
    

    void Start() {
        parentTransform = GameObject.Find("Board");
        SpawnBoardLocations();

        visualArrow = GameObject.Find("ArrowContainer");
        visualArrow.transform.position = new Vector3(PieceSpawnLocations[currentSelectionIndex].x, PieceSpawnLocations[currentSelectionIndex].y + 1.5f, PieceSpawnLocations[currentSelectionIndex].z);

        Robot = GameObject.Find("Robot");
        robotAnimator = Robot.GetComponent<Animator>();
        //spawn robot
        Robot.transform.position = new Vector3(PieceSpawnLocations[currentSelectionIndex].x, PieceSpawnLocations[currentSelectionIndex].y+0.5f, PieceSpawnLocations[currentSelectionIndex].z);
        roboOriginalLocation = Robot.transform.position;

        robotStatusText = GameObject.Find("ThinkingText").GetComponent<TextMesh>(); 
        gameStatusText = GameObject.Find("StatusText").GetComponent<Text>();
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

        for(int col = 0; col < 7; col++) PieceSpawnLocations[col] = VisualBoard[0, col].transform.position;
        
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

        if(isGameOver) return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            if (aiTurn) return;

            int r = PlacePiece(currentSelectionIndex, Board, MINIMIZER);
            if (r == -1) return;
            var go = Instantiate(minimizerPrefab, PieceSpawnLocations[currentSelectionIndex], minimizerPrefab.transform.rotation);
            go.GetComponent<Piece>().SetCol(VisualBoard[r, currentSelectionIndex].transform.position);
            placedPieces.AddFirst(go);
            if (Win(MINIMIZER))
            {
                StartCoroutine(RobotDeath());
                gameStatusText.text = "Player has won!";
                return;
            }

            robotThinking= true;
            StartCoroutine(AnimateRobotThinkingText());

            _t1 = new Thread(_PlaceAiPiece);
            if (!_t1.IsAlive) _t1.Start();
        }

        if (Input.GetKeyDown(KeyCode.F10)) //ai hack
        {

            _t1 = new Thread(_PlaceAiPiece);
            if (!_t1.IsAlive) _t1.Start();
        }

        if (Input.GetKeyDown(KeyCode.F11)) //player hack
        {
            int r = PlacePiece(currentSelectionIndex, Board, MINIMIZER);
            if (r == -1) return;
            var go = Instantiate(minimizerPrefab, PieceSpawnLocations[currentSelectionIndex], minimizerPrefab.transform.rotation);
            go.GetComponent<Piece>().SetCol(VisualBoard[r, currentSelectionIndex].transform.position);
            placedPieces.AddFirst(go);
            if (Win(MINIMIZER))
            {
                StartCoroutine(RobotDeath());
                gameStatusText.text = "Player has won!";
            }
        }

        if(Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            currentSelectionIndex--;
            if (currentSelectionIndex < 0) currentSelectionIndex = 6;
            visualArrow.transform.position = new Vector3(PieceSpawnLocations[currentSelectionIndex].x, PieceSpawnLocations[currentSelectionIndex].y + 1.5f, PieceSpawnLocations[currentSelectionIndex].z);
        }

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            currentSelectionIndex++;
            if (currentSelectionIndex >= 7) currentSelectionIndex = 0;
            visualArrow.transform.position = new Vector3(PieceSpawnLocations[currentSelectionIndex].x, PieceSpawnLocations[currentSelectionIndex].y + 1.5f, PieceSpawnLocations[currentSelectionIndex].z);
        }
    }


    private void _PlaceAiPiece()
    {
        aiTurn = true;
        aiColIndex = FindBestMove(Board, MaxDepth);
        aiRow = PlacePiece(aiColIndex, Board, MAXIMIZER);

        threadFin = true;
        robotThinking = false;
        Thread.CurrentThread.Abort();

    }

    void ResetGame()
    {
        for(int row = 0; row < 6; row++)
            for(int col = 0; col < 7; col++) Board[row, col] = 0;

        foreach (var go in placedPieces) Destroy(go);
        placedPieces.Clear();

        Robot.transform.position = roboOriginalLocation;
        robotAnimator.SetBool("dead", false);
        gameStatusText.text = "";
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
        Vector3 GoalPos = PieceSpawnLocations[aiColIndex];
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
        Destroy(Instantiate(animationPrefabs[rand], new Vector3(PieceSpawnLocations[aiColIndex].x, PieceSpawnLocations[aiColIndex].y+.5f, PieceSpawnLocations[aiColIndex].z), animationPrefabs[rand].transform.rotation), 2f);

        yield return new WaitForSeconds(0.75f);
        Robot.transform.position = new Vector3(PieceSpawnLocations[aiColIndex].x, PieceSpawnLocations[aiColIndex].y+.5f, PieceSpawnLocations[aiColIndex].z);
        robotAnimator.SetBool("isRun", false);
        robotAnimator.SetBool("attack", false);

        var go = Instantiate(maximizerPrefab, PieceSpawnLocations[aiColIndex], maximizerPrefab.transform.rotation);
        go.GetComponent<Piece>().SetCol(VisualBoard[aiRow, aiColIndex].transform.position);
        placedPieces.AddFirst(go);
        if (Win(MAXIMIZER))
        {
            Destroy(Instantiate(explosionPrefab), 5f);
            gameStatusText.text = "AI has won!";
        }

        aiTurn = false;

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
            }
        }
        return bestMoveColumnIndex;
    }

    int Minimax(int[,] board, int maxDepth, int currentDepth, bool isMax, int alpha, int beta)
    {
        int score = Evaluate(board) - (currentDepth);
        if (score >= winScore-20000) return score + currentDepth;
        if (score < loseScore+20000) return score - currentDepth; //minimizer won
        if (!IsMovesLeft() || currentDepth >= maxDepth) return score; //might be a tie? 

        if (isMax)
        {
            int best = int.MinValue;
            //traverse all cells
            for (int col = 0; col < Board.GetLength(1); col++)
                if (Board[0, col] == 0) //check if empty
                {
                    //make the move
                    int[,] tempBoard = CopyBoard(board);
                    PlacePiece(col, tempBoard, MAXIMIZER);
                    //call minimax recursively and choose the max value
                    int result = Minimax(tempBoard, maxDepth, currentDepth + 1, !isMax, alpha, beta);
                    if (result > best) best = result;
                    if(best > alpha) alpha = best;

                    if(beta <= alpha) break;
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
                    if(beta <= alpha) break;
                }
            return best;
        }
    }

    int[,] CopyBoard(int[,] board)
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

    const int winScore = 1000000;
    const int loseScore = -100000;
    int Evaluate(int[,] board) //Scoring function - There should be 69 possible ways to win on an empty board.
    {
        //1 - look at rows first (24 ways to win)
        for (int row = 0; row < board.GetLength(0); row++)
            for (int col = 0; col < 4; col++) //only need to look at 4 ways to win per  
                if ((board[row, col] == board[row, col + 1]) &&
                   (board[row, col] == board[row, col + 2]) &&
                   (board[row, col] == board[row, col + 3]))
                    if (board[row, col] == MAXIMIZER) return winScore;
                    else if (board[row, col] == MINIMIZER) return loseScore;

        //2 - look at vertical (3 * 7 = 21 ways)
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < board.GetLength(1); col++) //only need to look at 4 ways to win per  
                if ((board[row, col] == board[row + 1, col]) &&
                   (board[row, col] == board[row + 2, col]) &&
                   (board[row, col] == board[row + 3, col]))
                    if (board[row, col] == MAXIMIZER) return winScore;
                    else if (board[row, col] == MINIMIZER) return loseScore;

        //3 - look at diagonal right and down (12), right to up (12)
        for (int col = 0; col < 4; col++)
        {
            for (int row = 0; row < 3; row++)
                if ((board[row, col] == board[row + 1, col + 1]) &&
                    (board[row, col] == board[row + 2, col + 2]) &&
                    (board[row, col] == board[row + 3, col + 3]))
                    if (board[row, col] == MAXIMIZER) return winScore;
                    else if (board[row, col] == MINIMIZER) return loseScore;

            for (int row = 3; row < 6; row++)
                if ((board[row, col] == board[row - 1, col + 1]) &&
                    (board[row, col] == board[row - 2, col + 2]) &&
                    (board[row, col] == board[row - 3, col + 3]))
                    if (board[row, col] == MAXIMIZER)
                        return winScore;
                    else if (board[row, col] == MINIMIZER)
                        return loseScore;
        }

        //return 0;
        return GettingWinningMoveCount(board, MAXIMIZER) - GettingWinningMoveCount(board, MINIMIZER);
    }



    const int threeInARowValue = 30;
    const int twoInARow = 10;

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
            {
                if ((board[row, col] == 0 || board[row, col] == player) &&
                    (board[row + 1, col + 1] == 0 || board[row + 1, col + 1] == player) &&
                    (board[row + 2, col + 2] == 0 || board[row + 2, col + 2] == player) &&
                    (board[row + 3, col + 3] == 0 || board[row + 3, col + 3] == player))
                    winningMoves++;
            }

            for (int row = 3; row < 6; row++)
            {
                if ((board[row, col] == 0 || board[row, col] == player) &&
                    (board[row - 1, col + 1] == 0 || board[row - 1, col + 1] == player) &&
                    (board[row - 2, col + 2] == 0 || board[row - 2, col + 2] == player) &&
                    (board[row - 3, col + 3] == 0 || board[row - 3, col + 3] == player))
                    winningMoves++;
            }
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
            for (int col = 0; col < board.GetLength(1); col++) //only need to look at 4 ways to win per  
                if ((board[row, col] == player) &&
                   (board[row - 1, col] == player) &&
                   (board[row - 2, col] == 0) &&
                   (board[row - 3, col] == 0))
                    winningMoves+=twoInARow;

        /*
        //diagonal of 3s
        for (int col = 0; col < 4; col++)
        {
            for (int row = 0; row < 3; row++)
            {
                if ((board[row, col] == player) &&
                    (board[row + 1, col + 1] == player) &&
                    (board[row + 2, col + 2] == player) &&
                    (board[row + 3, col + 3] == 0))
                    winningMoves+=threeInARowValue;
                else if ((board[row, col] == player) &&
                    (board[row + 1, col + 1] == player) &&
                    (board[row + 2, col + 2] == 0) &&
                    (board[row + 3, col + 3] == player))
                    winningMoves+=threeInARowValue;
                else if ((board[row, col] == player) &&
                    (board[row + 1, col + 1] == 0) &&
                    (board[row + 2, col + 2] == player) &&
                    (board[row + 3, col + 3] == player))
                    winningMoves+=threeInARowValue;
                else if ((board[row, col] == 0) &&
                    (board[row + 1, col + 1] == player) &&
                    (board[row + 2, col + 2] == player) &&
                    (board[row + 3, col + 3] == player))
                    winningMoves+=threeInARowValue;
            }

            //other diagonal
            for (int row = 3; row < 6; row++)
            {
                if ((board[row, col] == player) &&
                    (board[row - 1, col + 1] == player) &&
                    (board[row - 2, col + 2] == player) &&
                    (board[row - 3, col + 3] == 0))
                    winningMoves+=threeInARowValue;
                else if ((board[row, col] == player) &&
                    (board[row - 1, col + 1] == player) &&
                    (board[row - 2, col + 2] == 0) &&
                    (board[row - 3, col + 3] == player))
                    winningMoves+=threeInARowValue;
                else if ((board[row, col] == player) &&
                    (board[row - 1, col + 1] == 0) &&
                    (board[row - 2, col + 2] == player) &&
                    (board[row - 3, col + 3] == player))
                    winningMoves+=threeInARowValue;
                else if ((board[row, col] == 0) &&
                    (board[row - 1, col + 1] == player) &&
                    (board[row - 2, col + 2] == player) &&
                    (board[row - 3, col + 3] == player))
                    winningMoves+=threeInARowValue;
            }
           
        }  */


        return winningMoves;
    }

    bool Win(int player)
    {
        //1 - look at rows first (24 ways to win)
        for (int row = 0; row < Board.GetLength(0); row++)
            for (int col = 0; col < 4; col++) //only need to look at 4 ways to win per  
                if ((Board[row, col] != 0 && Board[row, col] == player) &&
                   (Board[row, col] == Board[row, col + 1]) &&
                   (Board[row, col] == Board[row, col + 2]) &&
                   (Board[row, col] == Board[row, col + 3]))
                    return true;

        //2 - look at vertical (3 * 7 = 21 ways)
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < Board.GetLength(1); col++) //only need to look at 4 ways to win per  
                if ((Board[row, col] != 0 && Board[row, col] == player) &&
                   (Board[row, col] == Board[row + 1, col]) &&
                   (Board[row, col] == Board[row + 2, col]) &&
                   (Board[row, col] == Board[row + 3, col]))
                    return true;

        //3 - look at diagonal right and down (12), right to up (12)
        for (int col = 0; col < 4; col++)
        {
            for (int row = 0; row < 3; row++)
                if ((Board[row, col] != 0 && Board[row, col] == player) &&
                    (Board[row, col] == Board[row + 1, col + 1]) &&
                    (Board[row, col] == Board[row + 2, col + 2]) &&
                    (Board[row, col] == Board[row + 3, col + 3]))
                    return true;

            for (int row = 3; row < 6; row++)
                if ((Board[row, col] != 0 && Board[row, col] == player) &&
                    (Board[row, col] == Board[row - 1, col + 1]) &&
                    (Board[row, col] == Board[row - 2, col + 2]) &&
                    (Board[row, col] == Board[row - 3, col + 3]))
                    return true;
        }
        return false;
    }

}
