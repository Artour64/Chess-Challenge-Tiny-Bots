﻿using System;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot
{

    private Move[] movePreAlloc = new Move[256];
    private byte[] bytePreAlloc = new byte[256];
    private uint baseEvalCalls;//for debug

    
    //debug method, remove when done. This method and the calls account for 248 token brain capacity
    public void moveStats(String type, int depth, int full_depth, int eval, int startTime, Timer timer, bool isWhiteMove)
    {
        Console.WriteLine(
            (isWhiteMove ? "White  |  " : "Black  |  ")
            + type.PadRight(5)
            + "  |  depth: " + (depth + "(" + full_depth + ")").PadRight(7)
            + "  |  base eval calls: " + baseEvalCalls.ToString().PadRight(10)
            + "  |  eval: " + ((eval/2048.0)<0 ? "" + (eval/2048.0) : " "+(eval/2048.0)).PadRight(20)
            + "  |  time:" + ((startTime - timer.MillisecondsRemaining)/1000.0d) + "s"
            );
    }
    
    public Move Think(Board board, Timer timer)
    {
        baseEvalCalls = 0;///for debug
        
        bool isWhiteToMove = board.IsWhiteToMove;
        byte depth = 0;
        byte full_depth = 0;//depth reached in first time bracket, for debug, remove later
        int startTime = timer.MillisecondsRemaining;
        //*
        int timeLeftTargetLow = (startTime * 99)/100;
        int timeLeftTargetHigh = (startTime * 95)/100;//make smaller gap maybe
        /*/
        int timeLeftTargetLow = (startTime * 999)/1000;
        int timeLeftTargetHigh = (startTime * 99)/100;//make smaller gap maybe
        //*/

        // maybe add transposition table
        
        Move[] moves = moves_init_sorted(board.GetLegalMoves());
        int[] evals = new int[moves.Length];
        Move bestPrev = moves[0];//if eval is loosing, return this and hope opponent does not see mate

        while (true)
        {

            byte count = 0;
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
            byte i = 0;
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
    }
    
    private static readonly byte[] PIECE_VAL = {0,1,3,3,5,9,11};//king shouldn't matter
    
    //public static readonly byte[] PIECE_CAP_CAP = {0,2,8,4,4,8,8};//possible capture targets
    //public static readonly sbyte[] PIECE_VAL_RANK = {0,1,2,2,3,4,5};
    

    private int eval1(Board board)
    {
        //maybe add some center control eval
        
        baseEvalCalls++;//for debug
        
        
        //bool isWhiteToMove = board.IsWhiteToMove;
        
        int eval = 0;
        sbyte moveSign = boolToSign(board.IsWhiteToMove);
        byte pieceCount = 0;
        foreach (PieceList pieceList in board.GetAllPieceLists())
        {
            pieceCount += (byte)pieceList.Count;
            sbyte pieceSign = boolToSign(pieceList.IsWhitePieceList);
            byte pieceInt = (byte)pieceList.TypeOfPieceInList;
            
            eval +=
                pieceSign
                * PIECE_VAL[pieceInt]
                * pieceList.Count;
        }

        //how close pawns are to promotion
        if (pieceCount < 16)//is endgame
        {
            foreach (Piece piece in board.GetPieceList(PieceType.Pawn,true))
            {
                eval += piece.Square.Rank - 2;
            }
            foreach (Piece piece in board.GetPieceList(PieceType.Pawn,false))
            {
                eval += piece.Square.Rank - 7;
            }
        }

        eval *= 2048;
        
        Move[] moves = board.GetLegalMoves(true);

        if (moves.Length == 0)
        {
            if (board.IsInCheckmate())
            {
                return isMatedVal(board.IsWhiteToMove);
            }

            // if (board.IsDraw())
            // {
            //     return 0;
            // }
        }
        
        eval += moves.Length * moveSign;//squares controlled heuristic
        int cap = 0;
        foreach (Move move in moves)//captures only
        {
            int cap2 = moveCapVal(move);
            
            if (cap2 > cap)
            {
                cap = cap2;
            }
            eval += cap2 * moveSign * 4;//all possible captures
        }
        
        eval += cap * moveSign * 512;//best capture
        
        
        board.ForceSkipTurn();
        moves = board.GetLegalMoves(true);
        board.UndoSkipTurn();
        cap = 0;
        eval -= moves.Length * moveSign;//squares controlled heuristic
        foreach (Move move in moves)
        {
            int cap2 = moveCapVal(move);

            if (cap2 > cap)
            {
                cap = cap2;
            }

            eval -= cap2 * moveSign * 4;//all possible captures,negative for other color
        }
        
        eval -= cap * moveSign * 128;//best capture, negative for other color
        
        return eval;
    }

    private static sbyte boolToSign(bool b)
    {
        return (sbyte)(b ? 1 : -1);
    }
    
    private static int isMatedVal(bool colorIsWhite)
    {
        return -258048 * boolToSign(colorIsWhite);//-126 * 2048=-258048
    }

    private int evaln(Board board, int n, int best_eval, bool best_eval_equal) //best_eval is for alpha beta pruning
    {
        if (board.IsDraw())
        {
            return 0;
        }
        if (n == 0)
        {
            return eval1(board);//maybe do similar thing to Sebastian's bot where it does a capture only search here before doing base eval. Not sure how to consider when it's better not to capture (e.g. only suicidal captures available).
        }

        bool isWhiteToMove = board.IsWhiteToMove;

        Move[] moves = moves_init_sorted(board.GetLegalMoves());

        int eval = isMatedVal(isWhiteToMove);
        
        if (moves.Length == 0)
        {
            if (board.IsInCheckmate())
            {
                return eval;
            }
            //return 0;
        }
        
        //maybe do iterative deepening up to n-1, filter and sort each iteration, might improve alpha-beta pruning,
        //depth for loop here around the move loop
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
            if (isWhiteToMove)//there might be a way to remove duplication and maybe reduce branching
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
    
    private Move[] moves_init_sorted(Move[] moves)//helps alpha beta pruning
    {
        //maybe put checks first
        //return moves;
        
        byte countStart = 0;
        byte countEnd = (byte)(moves.Length - 1);
        bool hasCap = false;
        
        foreach (Move m in moves)
        {
            byte cap = moveCapVal(m);

            if (cap > 0)
            {
                hasCap = true;
                movePreAlloc[countStart] = m;
                bytePreAlloc[countStart] = (byte)(32-cap);//invert for backwards sort
                countStart++;
            }
            else
            {
                movePreAlloc[countEnd] = m;
                countEnd--;
            }
        }

        if (hasCap)//if no cap, nothing to sort
        {
            for (byte i = 0; i < moves.Length; i++)
            {
                moves[i] = movePreAlloc[i];
            }

            Array.Sort(bytePreAlloc, moves, 0,
                countStart);
        }

        return moves;
    }
    //*/

    private static byte moveCapVal(Move m)//also considers promotion value
    {
        return (byte) (
            PIECE_VAL[(byte)m.CapturePieceType]
            + PIECE_VAL[(byte)m.PromotionPieceType]
            - Convert.ToByte(m.IsPromotion)//if not a promotion, -1 for pawn that is replaced
            );
    }
    
}

