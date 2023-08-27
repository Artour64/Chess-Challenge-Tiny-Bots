using System;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot
{

    //private Move[] movePreAlloc = new Move[256];

    
    //this method and the calls account for 123 token brain capacity, more now, idk how much
    public static void moveStats(String type, int depth, int full_depth, int eval, int startTime, Timer timer, bool isWhiteMove)
    {
        Console.WriteLine((isWhiteMove ? "White  |  " : "Black  |  ")+ type + "  |  depth: " + depth + "("+full_depth+")  |  eval: " + (eval/2048.0) + "  |  time: " + (startTime - timer.MillisecondsRemaining) );
    }
    
    public Move Think(Board board, Timer timer)
    {
        Move[] moves = moves_init_sorted(board.GetLegalMoves());
        
        //Move[] bestMoves = new Move[moves.Length];
        int[] evals = new int[moves.Length];
        
        bool isWhiteToMove = board.IsWhiteToMove;
        int depth = 0;
        int full_depth = 0;//depth reached in first time bracket, for debug, remove later
        int startTime = timer.MillisecondsRemaining;
        int timeLeftTargetLow = (startTime * 995)/1000;
        int timeLeftTargetHigh = (startTime * 99)/100;//make smaller gap maybe

        Move bestPrev = moves[0];//if eval is loosing, return this and hope opponent does not see mate

        while (true)
        {

            int count = 0;
            int eval = isMatedVal(isWhiteToMove);
            
            foreach (Move m in moves)
            {
                
                board.MakeMove(m);
                int eval2 = evaln(board, depth, eval, false);
                board.UndoMove(m);
                
                if (eval2 == isMatedVal(!isWhiteToMove))
                {
                    moveStats("Win ",depth,full_depth,eval2,startTime,timer,isWhiteToMove);
                    return m;
                }

                evals[count] = eval2;
                count++;
                if (isWhiteToMove)
                {
                    if (eval2 > eval)
                    {
                        eval = eval2;
                    }
                }
                else
                {
                    if (eval2 < eval)
                    {
                        eval = eval2;
                    }
                }
            }

            if (eval == isMatedVal(isWhiteToMove))
            {
                moveStats("Lose",depth,full_depth,eval,startTime,timer,isWhiteToMove);
                return bestPrev;
            }
            
            //filter and sort
            
            Move[] bestMoves = new Move[moves.Length];//variable name is not accurate anymore
            int[] evals2 = new int[moves.Length];
            count = 0;
            int i = 0;
            foreach (Move c in moves)
            {
                if (timer.MillisecondsRemaining > timeLeftTargetLow)
                {
                    if (evals[i] != isMatedVal(isWhiteToMove))//non loosing moves
                    {
                        bestMoves[count] = c;
                        evals2[count] = evals[i];
                        count++;
                    }
                }
                else if (evals[i] == eval)//best moves
                {
                    bestMoves[count] = c;
                    count++;
                }

                i++;
            }

            int t = timer.MillisecondsRemaining;

            if (t > timeLeftTargetHigh)
            {
                if (count < moves.Length)
                {
                    Array.Resize(ref bestMoves, count);
                    Array.Resize(ref evals2, count);
                }

                moves = bestMoves;
                if (moves.Length == 1)
                {
                    moveStats("Best",depth,full_depth,eval,startTime,timer,isWhiteToMove);
                    return moves[0];
                }

                if (t > timeLeftTargetLow)
                {
                    full_depth++;
                    Array.Sort(evals2, moves);
                    if (!isWhiteToMove)
                    {
                        moves.Reverse();
                    }
                }

                bestPrev = moves[0];
            }
            else
            {
                moveStats("Rand",depth,full_depth,eval,startTime,timer,isWhiteToMove);
                return bestMoves[new Random().Next(count)];
            }
            

            depth++;
        }
        //Console.WriteLine(depth-1);
        //return Move.NullMove;
    }
    
    public static readonly byte[] PIECE_VAL = {0,1,3,3,5,9,11};//king shouldn't matter
    public static readonly byte[] PIECE_CAP_CAP = {0,2,8,4,4,8,8};//possible capture targets
    //public static readonly sbyte[] PIECE_VAL_RANK = {0,1,2,2,3,4,5};
    
    /*
    public static sbyte eval0(Board board)//method merged into eval1, will probably remove later
    {
        int eval = 0;
        foreach (PieceList pieceList in board.GetAllPieceLists())
        {
            eval +=
                boolToSign(pieceList.IsWhitePieceList)
                * PIECE_VAL[(byte)pieceList.TypeOfPieceInList]
                * pieceList.Count;
        }
        return (sbyte) (eval * 2);//*2 for odd numbers to represent half points
    }
    //*/

    public int eval1(Board board)
    {
        //Move[] moves = board.GetLegalMoves(true);
        int eval = 0;
        int alloc = 0;
        int alloc2 = 0;//computed but not used yet, for other player moves
        sbyte moveSign = boolToSign(board.IsWhiteToMove);
        foreach (PieceList pieceList in board.GetAllPieceLists())
        {
            byte pieceInt = (byte)pieceList.TypeOfPieceInList;
            eval +=
                boolToSign(pieceList.IsWhitePieceList)
                * PIECE_VAL[pieceInt]
                * pieceList.Count;
            
            int al = PIECE_CAP_CAP[pieceInt] * pieceList.Count;
            if (pieceList.IsWhitePieceList == board.IsWhiteToMove)
            {
                alloc += al;
            }
            else
            {
                alloc2 += al;
            }
        }

        eval *= 2048;
        
        System.Span<Move> moves = stackalloc Move[alloc];//112 is max captures possible (unrealistic circumstances)
        board.GetLegalMovesNonAlloc(ref moves,true);
        //does not seem to make a noticeable difference in performance here

        //bool isWhiteToMove = board.IsWhiteToMove;
        //*
        if (moves.Length == 0)
        {
            if (board.IsInCheckmate())
            {
                return isMatedVal(board.IsWhiteToMove);
            }

            if (board.IsDraw())
            {
                return 0;
            }
        }
        //*/
        //sbyte eval = eval0(board);
        eval += moves.Length * moveSign;
        int cap = 0;
        foreach (Move move in moves)//captures only
        {
            int cap2 = PIECE_VAL[(int)move.CapturePieceType];
            if (move.IsPromotion)
            {
                cap2 += 8;//9-1 assume queen promotion
            }
            if (cap2 > cap)
            {
                cap = cap2;
            }
            eval += cap2 * moveSign * 4;
        }

        
        // if (cap > 0)
        // {
        eval += cap * moveSign * 512;
        // }
        
        
        
        if (board.TrySkipTurn())
        {
            moves = stackalloc Move[alloc2];//112 is max captures possible (unrealistic circumstances)
            board.GetLegalMovesNonAlloc(ref moves, true);
            board.UndoSkipTurn();
            cap = 0;
            eval -= moves.Length * moveSign;
            foreach (Move move in moves) //captures only
            {
                int cap2 = PIECE_VAL[(int)move.CapturePieceType];
                if (move.IsPromotion)
                {
                    cap2 += 8; //9-1 assume queen promotion
                }

                if (cap2 > cap)
                {
                    cap = cap2;
                }

                eval -= cap2 * moveSign * 4;//negative for other color
            }


            // if (cap > 0)
            // {
            eval -= cap * moveSign * 128;//negative for other color
            // }
        }
        else//in check, atm is biggest value, subject to change, might use force skip instead
        {
            eval -= moveSign * 3072;//128*(9+9-1+1)=128*18=2304, make bigger, 2048+1024=3072
        }

        return eval;
    }

    public static sbyte boolToSign(bool b)
    {
        /*
        if (b)
        {
            return 1;
        }
        return -1;
        */
        return (sbyte)(b ? 1 : -1);//probably the same performance, might revert
    }
    
    static int isMatedVal(bool colorIsWhite)
    {
        return -126 * 2048 * boolToSign(colorIsWhite);
    }

    public int evaln(Board board, int n, int best_eval, bool best_eval_equal) //best_eval is for alpha beta pruning
    {
        if (n == 0)
        {
            return eval1(board);
        }

        bool isWhiteToMove = board.IsWhiteToMove;

        Move[] moves= moves_init_sorted(board.GetLegalMoves());

        int eval = isMatedVal(isWhiteToMove);
        
        if (moves.Length == 0)
        {
            if (board.IsInCheckmate())
            {
                return eval;
            }
            return 0;
        }
        
        foreach (Move m in moves)
        {
            board.MakeMove(m);
            int eval2 = evaln(board, n - 1, eval, true);
            board.UndoMove(m);
            if (eval2 == isMatedVal(!isWhiteToMove))
            {
                return eval2;
            }
            if (best_eval_equal && eval2 == best_eval)
            {
                return best_eval;
            }
            if (isWhiteToMove)
            {
                if (eval2 > eval)
                {
                    eval = eval2;
                    if (eval > best_eval)
                    {
                        return eval;
                    }
                }
            }
            else
            {
                if (eval2 < eval)
                {
                    eval = eval2;
                    if (eval < best_eval)
                    {
                        return eval;
                    }
                }
            }
        }

        return eval;
    }

    public static Move[] moves_init_sorted(Move[] moves)//helps alpha beta pruning
    {
        Move[] movesOut = new Move[moves.Length];
        int countStart = 0;
        int countEnd = moves.Length - 1;
        int[] evals = new int[moves.Length];
        
        foreach (Move m in moves)
        {
            byte cap = 0;
            if (m.IsCapture)
            {
                cap = PIECE_VAL[(byte)m.CapturePieceType];
            }

            if (m.IsPromotion)
            {
                cap += PIECE_VAL[(byte)m.PromotionPieceType];
                cap--;
            }

            if (cap > 0)
            {
                movesOut[countStart] = m;
                evals[countStart] = cap;
                countStart++;
            }
            else
            {
                movesOut[countEnd] = m;
                countEnd--;
            }
        }
        
        Array.Sort(evals,movesOut,0,countStart);
        return movesOut;
    }
    
}

