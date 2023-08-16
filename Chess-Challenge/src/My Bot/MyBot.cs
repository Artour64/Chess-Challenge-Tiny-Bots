using System;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        int depth = 3;
        
        Move[] moves = moves_init_sorted(board.GetLegalMoves());
        Move move = moves[0];
        Move[] bestMoves = new Move[moves.Length];
        int count = 1;
        bestMoves[0] = move;
        board.MakeMove(move);
        int eval = evaln(board, depth,127,false);//127 means first, don't do alpha beta pruning
        board.UndoMove(move);

        bool first = true;
        foreach (Move m in moves)
        {
            if (first)
            {
                first = false;
                continue;
            }
            board.MakeMove(m);
            int eval2 = evaln(board,depth,(sbyte)eval,false);
            board.UndoMove(m);
            if (eval2 == eval)
            {
                bestMoves[count] = m;
                count++;
            }
            else if (board.IsWhiteToMove)
            {
                if (eval2 > eval)
                {
                    eval = eval2;
                    bestMoves[0] = m;
                    count = 1;
                }
            }
            else
            {
                if (eval2 < eval)
                {
                    eval = eval2;
                    bestMoves[0] = m;
                    count = 1;
                }
            }
        }
        
        return bestMoves[new Random().Next(count)];
    }
    
    public static readonly sbyte[] PIECE_VAL = {0,1,3,3,5,9,99};//king shouldn't matter
    //public static readonly sbyte[] PIECE_VAL_RANK = {0,1,2,2,3,4,5};
    public static sbyte eval0(Board board)
    {
        sbyte eval = 0;
        foreach (PieceList pieceList in board.GetAllPieceLists())
        {
            eval += (sbyte) (
                boolToSign(pieceList.IsWhitePieceList)
                * PIECE_VAL[(byte)pieceList.TypeOfPieceInList]
                * pieceList.Count
                * 2);//*2 for odd numbers to represent half points
        }

        return eval;
    }

    public static sbyte eval1(Board board)
    {
        Move[] moves = board.GetLegalMoves(true);
        //*
        if (moves.Length == 0)
        {
            if (board.IsInCheckmate())
            {
                return (sbyte) (-126 * boolToSign(board.IsWhiteToMove));
            }

            if (board.IsDraw())
            {
                return 0;
            }
        }
        //*/
        sbyte eval = eval0(board);
        int cap = 0;
        foreach (Move move in moves)
        {
            int cap2 = (int)move.CapturePieceType;
            if (move.IsPromotion)
            {
                cap += 8;//9-1 assume queen promotion
            }
            if (cap2 > cap)
            {
                cap = cap2;
            }
        }

        /*//not worth the brain capacity cost
        //promotion, does not check if move is legal which may me inaccurate
        if (cap < 8)
        {
            foreach (Piece piece in board.GetPieceList(PieceType.Pawn, board.IsWhiteToMove))
            {
                int rank = piece.Square.Rank;
                int file = piece.Square.File;
                if (board.IsWhiteToMove)
                {
                    if (rank == 6)
                    {
                        if (board.GetPiece(new Square(file, 7)).IsNull)
                        {
                            eval = 8;
                            break;
                        }
                    }
                }
                else
                {
                    if (rank == 6)
                    {
                        if (board.GetPiece(new Square(file, 0)).IsNull)
                        {
                            eval = 8;
                            break;
                        }
                    }
                }
            }

            
        }
        */

        eval += (sbyte)cap;//eval is in whole points (*2), cap is in half points (1), overall means average of current and best next position
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

    public static bool mateHunt(Board board, int n, bool hunterIsWhite)
    {
        bool isHunter = board.IsWhiteToMove == hunterIsWhite;
        if (board.IsInCheckmate())
        {
            return !isHunter;
        }
        if (n <= 0)
        {
            return false;
        }
        Move[] moves = board.GetLegalMoves();
        
        //is hunter, looking for mate
        if (isHunter)
        {
            for (int c=0; c<n; c++)
            {
                foreach (Move m in moves)
                {
                    board.MakeMove(m);
                    bool huntRes = mateHunt(board, c, hunterIsWhite);
                    board.UndoMove(m);
                    if (huntRes)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        
        
        //not hunter, avoiding mate
        foreach (Move m in moves)
        {
            board.MakeMove(m);
            bool huntRes = mateHunt(board, n, hunterIsWhite);
            board.UndoMove(m);
            if (!huntRes)
            {
                return false;
            }
        }

        return true;
    }

    public static sbyte evaln(Board board, int n, sbyte best_eval, bool best_eval_equal) //best_eval is for alpha beta pruning
    {
        if (n == 0)
        {
            return eval1(board);
        }
        
        if (mateHunt(board, (n+1)/2, board.IsWhiteToMove))
        {
            return (sbyte) (126 * boolToSign(board.IsWhiteToMove));
        }
        // if (mateHunt(board, n/2, !board.IsWhiteToMove))
        // {
        //     return (sbyte) (-126 * boolToSign(board.IsWhiteToMove));
        // }
        
        Move[] moves = moves_init_sorted(board.GetLegalMoves());
        if (moves.Length == 0)
        {
            if (board.IsInCheckmate())
            {
                return (sbyte) (-126 * boolToSign(board.IsWhiteToMove));
            }
            return 0;
        }
        Move move = moves[0];
        board.MakeMove(move);
        int eval = evaln(board, n - 1, 127,true);//first, don't do pruning
        board.UndoMove(move);

        if (eval == 126 * boolToSign(board.IsWhiteToMove))
        {
            return (sbyte)(126 * boolToSign(board.IsWhiteToMove));
        }

        if (best_eval != 127)
        {
            if (best_eval_equal && eval == best_eval)
            {
                return best_eval;
            }
            if (board.IsWhiteToMove)
            {
                if (eval >= best_eval)
                {
                    return (sbyte)eval;
                }
            }
            else
            {
                if (eval <= best_eval)
                {
                    return (sbyte)eval;
                }
            }
        }

        bool first = true;
        foreach (Move m in moves)
        {
            if (first)
            {
                first = false;
                continue;
            }
            board.MakeMove(m);
            int eval2 = evaln(board, n - 1, (sbyte)eval, true);
            board.UndoMove(m);
            if (best_eval_equal && eval2 == best_eval)
            {
                return best_eval;
            }
            if (board.IsWhiteToMove)
            {
                if (eval2 > eval)
                {
                    eval = eval2;
                    if (eval > best_eval && best_eval != 127)
                    {
                        return (sbyte)eval;
                    }
                }
            }
            else
            {
                if (eval2 < eval)
                {
                    eval = eval2;
                    if (eval < best_eval && best_eval != 127)
                    {
                        return (sbyte)eval;
                    }
                }
            }
        }

        return (sbyte)eval;
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
        
        System.Array.Sort(evals,movesOut,0,countStart);
        return movesOut;
    }
}

