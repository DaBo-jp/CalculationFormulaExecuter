using System;
using System.Collections.Generic;
using System.Linq;

namespace CalculationFormulaExecuterLibrary
{

    internal class OperatorPriority
    {
        // 演算子の優先順位を静的辞書で管理
        private static readonly Dictionary<string, int> _operatorPriorities = new Dictionary<string, int>
        {
            { "*", 5 }, { "/", 5 },
            { "+", 4 }, { "-", 4 },
            { "?", 3 }, { @"\", 3 }, { "<", 3 }, { ">", 3 },
            { "&", 2 }, { "|", 2 },
            { "!", 1 }, { "=", 1 },
            { "_", 6 } // 単項マイナスの優先度を高く設定 (例: -5 + 3 の場合、-5 が先に計算される)
        };

        /// <summary>
        /// 指定された演算子の優先順位を取得します。
        /// </summary>
        /// <param name="operatorSymbol">演算子を示す文字列。</param>
        /// <returns>演算子の優先順位。見つからない場合は int.MaxValue を返します。</returns>
        public static int GetPriority(string operatorSymbol)
        {
            if (_operatorPriorities.TryGetValue(operatorSymbol, out int priority))
            {
                return priority;
            }
            return int.MaxValue; // 演算子が見つからない場合は最大値 (最も低い優先度と解釈)
        }
    }

    internal class OperationTree
    {
        private string TargetFormula_;
        public string TargetFormula { set => TargetFormula_ = value; }

        private OperationTree Left_;
        private OperationTree Right_;

        // 演算子優先順位は静的クラスから取得するため、このフィールドは不要になりました
        // OperatorPriority Priority_;

        public OperationTree() : this(string.Empty) { }
        public OperationTree(string targetFormula_)
        {
            TargetFormula_ = targetFormula_;
            Left_ = null;
            Right_ = null;
        }

        /// <summary>
        /// 計算式を前処理します。
        /// 複合演算子の置換、スペースの除去、論理演算子の置換を行います。
        /// </summary>
        /// <param name="targetFormula">処理対象の計算式。</param>
        /// <returns>前処理後の計算式。</returns>
        private string PreGrowthProcess(string targetFormula)
        {
            targetFormula = targetFormula.Replace(@"--", @"+");
            targetFormula = targetFormula.Replace(@"+-", @"-");
            targetFormula = targetFormula.Replace(@">=", @"?");
            targetFormula = targetFormula.Replace(@"<=", @"\");
            targetFormula = targetFormula.Replace(@"!=", @"!");
            targetFormula = targetFormula.Replace(@" ", @"");
            targetFormula = targetFormula.Replace(@"　", @""); // 全角スペースも除去
            targetFormula = targetFormula.Replace(@"And", @"&");
            targetFormula = targetFormula.Replace(@"Or", @"|");
            return targetFormula;
        }

        /// <summary>
        /// 計算式が最外側で括弧で囲まれており、かつその括弧が式全体をカバーしているかを判断します。
        /// </summary>
        /// <param name="targetFormula">対象の計算式。</param>
        /// <returns>最外側が有効な括弧で囲まれていれば true、そうでなければ false。</returns>
        private bool MostOutersAreBrackets(string targetFormula)
        {
            if (string.IsNullOrEmpty(targetFormula))
            {
                return false;
            }

            // 最初の文字が '(' で、最後の文字が ')' であるか
            bool hasOuterBrackets = targetFormula.First() == '(' && targetFormula.Last() == ')';
            if (!hasOuterBrackets)
            {
                return false;
            }

            // 括弧のネストが有効であるか (最終的に深度が0になるか)
            if (!ValidatesNest(targetFormula))
            {
                return false;
            }

            // 最初の括弧のペアが式全体を囲んでいるか (ネスト深度が0になるポイントが式の最後であるか)
            return NestDropPoint(targetFormula) == targetFormula.Length - 1;
        }

        /// <summary>
        /// 式内の括弧のネスト深度が0になる最初のポイントを検索します。
        /// </summary>
        /// <param name="formula">対象の計算式。</param>
        /// <returns>ネスト深度が0になる最初のインデックス。見つからない場合は式の長さ。</returns>
        private int NestDropPoint(string formula)
        {
            int lowerDepth = int.MaxValue;
            int currentDepth = 0;
            int i = 0;
            while (i < formula.Length)
            {
                UpdateDepth(formula[i], ref currentDepth);
                if (lowerDepth > currentDepth)
                {
                    lowerDepth = currentDepth;
                }
                if (lowerDepth == 0) // 深度が0になったらその時点で終了
                {
                    break;
                }
                ++i;
            }
            return i;
        }

        /// <summary>
        /// 式の中で最も優先順位の低い演算子（かつ、括弧の外にある）のインデックスを検索します。
        /// 同じ優先順位の演算子が複数ある場合、一番右側の演算子を優先します。
        /// </summary>
        /// <param name="formula">対象の計算式。</param>
        /// <returns>最も優先順位の低い演算子のインデックス。見つからない場合は -1。</returns>
        private int LookupMostLowerOperatorIndex(string formula)
        {
            if (string.IsNullOrWhiteSpace(formula))
            {
                return -1;
            }
            int returnIndex = -1;
            int currentLowerPriority = int.MaxValue;
            int depth = 0;

            // 後ろからスキャンして、同じ優先順位の演算子がある場合は一番右側のものを取得
            for (int i = formula.Length - 1; i >= 0; --i)
            {
                UpdateDepth(formula[i], ref depth); // 逆順で深度を更新

                // 深度が0（最上位レベル）の演算子のみを考慮
                if (depth == 0)
                {
                    int priorityTmp = OperatorPriority.GetPriority(formula[i].ToString());

                    // 単項マイナスの判定:
                    // - 演算子が '-' で、かつ
                    // - 式の先頭であるか、または
                    // - 前の文字が演算子または開き括弧である場合
                    bool isUnaryMinusCandidate = false;
                    if (formula[i] == '-')
                    {
                        if (i == 0 ||
                            (i > 0 && (OperatorPriority.GetPriority(formula[i-1].ToString()) != int.MaxValue || formula[i-1] == '(')))
                        {
                            // ただし、直前が数値や閉じ括弧の場合は二項演算子
                            if (i > 0 && (char.IsDigit(formula[i-1]) || formula[i-1] == ')'))
                            {
                                isUnaryMinusCandidate = false;
                            }
                            else
                            {
                                isUnaryMinusCandidate = true;
                            }
                        }
                    }

                    if (isUnaryMinusCandidate)
                    {
                        // 単項マイナスは高い優先度で処理
                        priorityTmp = OperatorPriority.GetPriority("_"); // "_" を単項マイナスを示す特別な演算子として定義
                    }
                    else if (priorityTmp == int.MaxValue)
                    {
                        // 演算子ではない文字 (数値や変数など) はスキップ
                        continue;
                    }

                    // 現在見つかった演算子の優先順位が、これまでの最も低い優先順位以下であれば更新
                    if (priorityTmp <= currentLowerPriority)
                    {
                        currentLowerPriority = priorityTmp;
                        returnIndex = i;
                    }
                }
            }

            return returnIndex;
        }

        /// <summary>
        /// 最外側の括弧を繰り返し取り除きます。
        /// </summary>
        /// <param name="formula">対象の計算式。</param>
        /// <returns>括弧が取り除かれた計算式。</returns>
        private string ExplicitFormulaFromBracket(string formula)
        {
            while (MostOutersAreBrackets(formula))
            {
                formula = formula.Substring(1, (formula.Length - 2));
                if (string.IsNullOrEmpty(formula))
                {
                    break;
                }
            }
            return formula;
        }

        /// <summary>
        /// 文字が括弧であるかによってネスト深度を更新します。
        /// </summary>
        /// <param name="target">対象の文字。</param>
        /// <param name="depth">現在のネスト深度（参照渡し）。</param>
        private void UpdateDepth(in char target, ref int depth)
        {
            if (target == '(')
            {
                depth++;
            }
            if (target == ')')
            {
                depth--;
            }
        }

        /// <summary>
        /// 演算子の優先順位と深度に基づいて、その演算子が現在の最も低い優先順位の演算子の候補であるかを判断します。
        /// </summary>
        /// <param name="depth">現在のネスト深度。</param>
        /// <param name="priority">対象の演算子の優先順位。</param>
        /// <param name="currentPriority">現在見つかっている最も低い優先順位。</param>
        /// <returns>候補であれば true、そうでなければ false。</returns>
        private bool IsCandidate(int depth, int priority, int currentPriority)
        {
            // 演算子であり（priority != int.MaxValue）、
            // 最上位レベルにあり（depth == 0）、
            // これまでの最も低い優先順位以下である（priority <= currentPriority）
            return (priority != int.MaxValue) && (depth == 0) && (priority <= currentPriority);
        }

        /// <summary>
        /// 計算式を解析し、二分木構造を構築します。
        /// </summary>
        /// <returns>ツリーの構築が成功すれば true、失敗すれば false。</returns>
        public bool GrowthTree()
        {
            TargetFormula_ = PreGrowthProcess(TargetFormula_);
            TargetFormula_ = ExplicitFormulaFromBracket(TargetFormula_);

            if (string.IsNullOrEmpty(TargetFormula_))
            {
                // 空の式はツリー構築に失敗
                return false;
            }

            int operatorIndex = LookupMostLowerOperatorIndex(TargetFormula_);

            if (operatorIndex == -1)
            {
                // 演算子が見つからなければ、このノードは数値または変数
                // そのまま値を保持して終了
                return true;
            }

            // 見つかった演算子が単項マイナスの場合の特殊処理
            if (TargetFormula_[operatorIndex].ToString() == "-" && OperatorPriority.GetPriority("_") == OperatorPriority.GetPriority(TargetFormula_[operatorIndex].ToString()))
            {
                // 単項マイナスのノードとして設定
                Left_ = null; // 単項演算子なので左の子はなし
                Right_ = new OperationTree(TargetFormula_.Substring(operatorIndex + 1));
                if (!Right_.GrowthTree()) return false;
                TargetFormula_ = "_"; // 内部的には単項マイナスを表す記号に変更
                return true;
            }

            // 二項演算子の場合
            // 演算子の左側と右側に子ノードを生成
            Left_ = new OperationTree(TargetFormula_.Substring(0, operatorIndex));
            if (!Left_.GrowthTree()) return false; // 左の子のツリー構築が失敗したらfalseを返す

            Right_ = new OperationTree(TargetFormula_.Substring(operatorIndex + 1));
            if (!Right_.GrowthTree()) return false; // 右の子のツリー構築が失敗したらfalseを返す

            // 現在のノードには演算子を保持
            TargetFormula_ = TargetFormula_.Substring(operatorIndex, 1);

            return true;
        }

        /// <summary>
        /// 計算式内の括弧のネストが有効であるかを検証します。
        /// </summary>
        /// <param name="formula">対象の計算式。</param>
        /// <returns>括弧のネストが正しければ true、そうでなければ false。</returns>
        public bool ValidatesNest(string formula)
        {
            int depth = 0;

            foreach (char charactor in formula)
            {
                UpdateDepth(charactor, ref depth);
                if (depth < 0) // 閉じ括弧が開き括弧より先に来た場合
                {
                    return false;
                }
            }
            return (0 == depth); // 最終的に深度が0であれば有効
        }

        /// <summary>
        /// 構文木を後置順 (Post-order) で走査し、結果をリストに追加します。
        /// </summary>
        /// <param name="targetList">走査結果を追加するリスト。</param>
        public void GetTraverseStack(ref List<string> targetList)
        {
            if (Left_ != null)
            {
                Left_.GetTraverseStack(ref targetList);
            }

            if (Right_ != null)
            {
                Right_.GetTraverseStack(ref targetList);
            }
            targetList.Add(TargetFormula_);
        }
    }

    public class CalculationFormulaExecuter
    {
        private const string ERR_MSG = "A format does wrong.";
        public string ERR_STRING { get => ERR_MSG; }
        private readonly Dictionary<string, Func<string, string, decimal>> FourArismeticOperation;
        private readonly Dictionary<string, Func<string, decimal>> UnaryOperations; // 単項演算子用
        private OperationTree ResolvTree_;

        public CalculationFormulaExecuter()
        {
            ResolvTree_ = new OperationTree();

            // 二項演算子
            FourArismeticOperation =
                new Dictionary<string, Func<string, string, decimal>>
                {
                    { @"*", (string left, string right)=>{ return decimal.Parse(left) * decimal.Parse(right); } },
                    { @"/", (string left, string right)=>{ 
                        decimal divisor = decimal.Parse(right);
                        if (divisor == 0) throw new DivideByZeroException("Division by zero.");
                        return decimal.Parse(left) / divisor; 
                    } },
                    { @"+", (string left, string right)=>{ return decimal.Parse(left) + decimal.Parse(right); } },
                    { @"-", (string left, string right)=>{ return decimal.Parse(left) - decimal.Parse(right); } },
                    { @"?", (string left, string right)=>{ return (decimal.Parse(left) >= decimal.Parse(right)) ? 1m : 0m; } }, // >=
                    { @"\", (string left, string right)=>{ return (decimal.Parse(left) <= decimal.Parse(right)) ? 1m : 0m; } }, // <=
                    { @"<", (string left, string right)=>{ return (decimal.Parse(left) < decimal.Parse(right)) ? 1m : 0m; } },
                    { @">", (string left, string right)=>{ return (decimal.Parse(left) > decimal.Parse(right)) ? 1m : 0m; } },
                    { @"&", (string left, string right)=>{ return (decimal.Parse(left) != 0m && decimal.Parse(right) != 0m) ? 1m : 0m; } }, // AND
                    { @"|", (string left, string right)=>{ return (decimal.Parse(left) != 0m || decimal.Parse(right) != 0m) ? 1m : 0m; } }, // OR
                    { @"=", (string left, string right)=>{ return (decimal.Parse(left) == decimal.Parse(right)) ? 1m : 0m; } },
                    { @"!", (string left, string right)=>{ return (decimal.Parse(left) != decimal.Parse(right)) ? 1m : 0m; } },
                };

            // 単項演算子
            UnaryOperations = new Dictionary<string, Func<string, decimal>>
            {
                { "_", (string value) => { return -decimal.Parse(value); } } // 単項マイナス
            };
        }

        /// <summary>
        /// 与えられた計算式を評価し、結果を文字列で返します。
        /// </summary>
        /// <param name="targetFormula">評価する計算式。</param>
        /// <returns>計算結果の文字列、またはエラーメッセージ。</returns>
        public string DoesCalculate(in string targetFormula)
        {
            Stack<string> calculationStack = new Stack<string>();
            List<string> resolvedFormula = new List<string>();
            string returnString = string.Empty;

            ResolvTree_.TargetFormula = targetFormula;

            // 括弧のネストが不正な場合
            if (!ResolvTree_.ValidatesNest(targetFormula))
            {
                return ERR_MSG;
            }

            // 構文木の構築に失敗した場合
            try
            {
                if (!ResolvTree_.GrowthTree())
                {
                    // 式が単一の数値である場合や、解析できない場合など
                    if (decimal.TryParse(targetFormula, out decimal result))
                    {
                        return result.ToString();
                    }
                    return ERR_MSG;
                }
            }
            catch (Exception ex)
            {
                // その他の解析中の予期せぬエラー
                Console.WriteLine($"Error during tree growth: {ex.Message}"); // デバッグ用
                return ERR_MSG;
            }


            ResolvTree_.GetTraverseStack(ref resolvedFormula);

            foreach (string value in resolvedFormula)
            {
                if (FourArismeticOperation.ContainsKey(value)) // 二項演算子
                {
                    if (calculationStack.Count < 2)
                    {
                        return ERR_MSG; // 演算子が足りない
                    }
                    string right = calculationStack.Pop();
                    string left = calculationStack.Pop();

                    if (!decimal.TryParse(left, out decimal resultLeft) || !decimal.TryParse(right, out decimal resultRight))
                    {
                        return ERR_MSG; // 数値への変換に失敗
                    }
                    try
                    {
                        calculationStack.Push(FourArismeticOperation[value](left, right).ToString());
                    }
                    catch (DivideByZeroException)
                    {
                        return "Division by zero.";
                    }
                }
                else if (UnaryOperations.ContainsKey(value)) // 単項演算子
                {
                    if (calculationStack.Count < 1)
                    {
                        return ERR_MSG; // 演算対象が足りない
                    }
                    string operand = calculationStack.Pop();
                    if (!decimal.TryParse(operand, out decimal resultOperand))
                    {
                        return ERR_MSG; // 数値への変換に失敗
                    }
                    calculationStack.Push(UnaryOperations[value](operand).ToString());
                }
                else // 数値や変数
                {
                    // ここで変数の置換などを行う場合は、Dictionary<string, decimal> で変数値を保持するなど
                    // 適切な処理を追加する必要があります。
                    // 今回は数値としてのみ扱うため、そのままスタックにプッシュします。
                    if (!decimal.TryParse(value, out _))
                    {
                        // 数値としてパースできない場合はエラー
                        return ERR_MSG;
                    }
                    calculationStack.Push(value);
                }
            }

            if (calculationStack.Count == 1)
            {
                return calculationStack.Pop();
            }
            else
            {
                return ERR_MSG; // 式の評価が完了せず、スタックに複数の要素が残っている場合など
            }
        }
    }
}
