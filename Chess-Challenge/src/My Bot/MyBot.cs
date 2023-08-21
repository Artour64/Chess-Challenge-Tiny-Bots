using System;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        
        //Console.WriteLine("-");
        Move[] moves = moves_init_sorted(board.GetLegalMoves());
        
        //Move[] bestMoves = new Move[moves.Length];
        sbyte[] evals = new sbyte[moves.Length];
        
        bool isWhiteToMove = board.IsWhiteToMove;
        int depth = 0;
        int timeLeftTargetLow = (timer.MillisecondsRemaining * 999)/1000;
        int timeLeftTargetHigh = (timer.MillisecondsRemaining * 99)/100;//make smaller gap maybe

        Move bestPrev = moves[0];//if eval is loosing, return this and hope opponent does not see mate

        while (true)
        {

            int count = 0;
            sbyte eval = isMatedVal(isWhiteToMove);
            
            foreach (Move m in moves)
            {
                
                board.MakeMove(m);
                sbyte eval2 = evaln(board, depth, eval, false);
                board.UndoMove(m);
                
                if (eval2 == isMatedVal(!isWhiteToMove))
                {
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
            
            

            if (timer.MillisecondsRemaining > timeLeftTargetHigh)
            {
                Array.Resize(ref bestMoves, count);
                Array.Resize(ref evals2, count);
                moves = bestMoves;
                if (moves.Length == 1)
                {
                    //Console.WriteLine(depth);
                    return moves[0];
                }

                if (timer.MillisecondsRemaining > timeLeftTargetLow)
                {
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
                //Console.WriteLine(depth);
                return bestMoves[new Random().Next(count)];
            }
            

            depth++;
        }
        //Console.WriteLine(depth-1);
        //return Move.NullMove;
    }
    
    public static readonly sbyte[] PIECE_VAL = {0,1,3,3,5,9,99};//king shouldn't matter
    //public static readonly sbyte[] PIECE_VAL_RANK = {0,1,2,2,3,4,5};
    public static sbyte eval0(Board board)
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

    public static sbyte eval1(Board board)
    {
        Move[] moves = board.GetLegalMoves(true);

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
        sbyte eval = eval0(board);
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
        }

        
        if (cap > 0)
        {
            cap *= 2;
            cap--;//caps at final depth eval are worth half a piece less.
            cap *= boolToSign(board.IsWhiteToMove);
            return (sbyte)( eval + cap );
        }
        return eval;
    }

    public static sbyte boolToSign(bool b)
    {
        if (b)
        {
            return 1;
        }
        return -1;
    }
    
    static sbyte isMatedVal(bool colorIsWhite)
    {
        return (sbyte) (-126 * boolToSign(colorIsWhite));
    }

    public static sbyte evaln(Board board, int n, sbyte best_eval, bool best_eval_equal) //best_eval is for alpha beta pruning
    {
        if (n == 0)
        {
            return eval1(board);
        }

        bool isWhiteToMove = board.IsWhiteToMove;
        
        Move[] moves = moves_init_sorted(board.GetLegalMoves());
        
        sbyte eval = isMatedVal(isWhiteToMove);
        
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
            sbyte eval2 = evaln(board, n - 1, eval, true);
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
        sbyte[] evals = new sbyte[moves.Length];
        
        foreach (Move m in moves)
        {
            sbyte cap = 0;
            if (m.IsCapture)
            {
                cap = PIECE_VAL[(int)m.CapturePieceType];
            }

            if (m.IsPromotion)
            {
                cap += PIECE_VAL[(int)m.PromotionPieceType];
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

