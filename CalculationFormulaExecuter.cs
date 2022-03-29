using System;
using System.Collections.Generic;
using System.Linq;

namespace CalculationFormulaExecuterLibrary
{

    internal class OperatorPriority
    {
        public readonly string Operator_;
        public readonly int priority_;
        OperatorPriority Next;

        public OperatorPriority(string operator_, int priority_, OperatorPriority next)
        {
            Operator_ = operator_;
            this.priority_ = priority_;
            Next = next;
        }

        public int ReturnMyPriority(string operator_, int currentPriority)
        {
            if (Operator_ == operator_)
            {
                return priority_;
            }
            if (Next == null)
            {
                return currentPriority;
            }
            return Next.ReturnMyPriority(operator_, currentPriority);
        }
    }

    internal class OperationTree
    {
        private string TargetFormula_;
        public string TargetFormula { set => TargetFormula_ = value; }

        private OperationTree Left_;
        private OperationTree Right_;

        OperatorPriority Priority_;

        public OperationTree() : this(string.Empty) { }
        public OperationTree(string targetFormula_)
        {
            TargetFormula_ = targetFormula_;
            Priority_ =
                new OperatorPriority(@"*", 5,
                new OperatorPriority(@"/", 5,
                new OperatorPriority(@"+", 4,
                new OperatorPriority(@"-", 4,
                new OperatorPriority(@"?", 3, // >=
                new OperatorPriority(@"\", 3, // <=
                new OperatorPriority(@"<", 3,
                new OperatorPriority(@">", 3,
                new OperatorPriority(@"&", 2,
                new OperatorPriority(@"|", 2,
                new OperatorPriority(@"!", 1,
                new OperatorPriority(@"=", 1,
                null
                ))))))))))));

            Left_ = null;
            Right_ = null;
        }

        private string PreGrowthProcess(string targetFormula)
        {
            targetFormula = targetFormula.Replace(@"--", @"+");
            targetFormula = targetFormula.Replace(@"+-", @"-");
            targetFormula = targetFormula.Replace(@">=", @"?");
            targetFormula = targetFormula.Replace(@"<=", @"\");
            targetFormula = targetFormula.Replace(@"!=", @"!");
            targetFormula = targetFormula.Replace(@" ", @"");
            targetFormula = targetFormula.Replace(@"　", @"");
            targetFormula = targetFormula.Replace(@"And", @"&");
            targetFormula = targetFormula.Replace(@"Or", @"|");
            return targetFormula;
        }

        private bool MostOutersAreBrackets(string targetFormula)
        {

            bool returnBool = false;
            if (!string.IsNullOrEmpty(targetFormula))
            {
                returnBool = targetFormula.First() == '(';
                returnBool &= targetFormula.Last() == ')';
                returnBool &= ValidatesNest(targetFormula);
                returnBool &= NestDropPoint(targetFormula) == targetFormula.Length - 1;
            }
            return returnBool;
        }

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
                if (lowerDepth == 0)
                {
                    break;
                }
                ++i;
            }
            return i;
        }

        private int LookupMostLowerOperatorIndex(string formula)
        {
            if (string.IsNullOrWhiteSpace(formula))
            {
                return -1;
            }
            int returnIndex = -1;
            int currentLowerPriority = int.MaxValue;
            int depth = 0;

            for (int i = 0; i < formula.Length; ++i)
            {
                int priorityTmp = int.MaxValue;
                UpdateDepth(formula[i], ref depth);
                priorityTmp = Priority_.ReturnMyPriority(
                        formula[i].ToString(),
                        priorityTmp
                    );

                if (IsCandidate(depth, priorityTmp, currentLowerPriority))
                {
                    currentLowerPriority = priorityTmp;
                    returnIndex = i;
                }
            }

            return returnIndex;
        }

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

        private bool IsCandidate(int depth, int priority, int currentPriority)
        {
            return
                (priority != int.MaxValue) &
                (depth == 0) &
                (priority <= currentPriority);
        }

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

        public OperationTree(string targetFormula_, OperatorPriority priority_)
        {
            TargetFormula_ = targetFormula_;
            Priority_ = priority_;
            Left_ = null;
            Right_ = null;
        }

        public bool GrowthTree()
        {
            TargetFormula_ = PreGrowthProcess(TargetFormula_);
            TargetFormula_ = ExplicitFormulaFromBracket(TargetFormula_);

            if (string.IsNullOrEmpty(TargetFormula_))
            {
                return false;
            }

            int OperatorIndex = LookupMostLowerOperatorIndex(TargetFormula_);
            if (OperatorIndex == 0 && TargetFormula_[OperatorIndex] == '-')
            {
                return true;
            }

            bool returnBool =
                OperatorIndex != 0 &
                OperatorIndex != TargetFormula_.Length - 1 |
                TargetFormula_.Length == 1;

            if (OperatorIndex > 0)
            {
                if (returnBool)
                {
                    Left_ = new OperationTree(TargetFormula_.Substring(0, OperatorIndex), Priority_);
                    returnBool &= Left_.GrowthTree();

                    Right_ = new OperationTree(TargetFormula_.Substring(OperatorIndex + 1), Priority_);
                    returnBool &= Right_.GrowthTree();

                    TargetFormula_ = TargetFormula_.Substring(OperatorIndex, 1);
                }
            }
            return returnBool;
        }

        public bool ValidatesNest(string formula)
        {
            int depth = 0;

            foreach (char charactor in formula)
            {
                UpdateDepth(charactor, ref depth);
            }
            return (0 == depth);
        }

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
        private readonly string ERR_MSG = "A format does wrong.";
        public string ERR_STRING { get => ERR_MSG; }
        private readonly Dictionary<string, Func<string, string, decimal>> FourArismeticOperation;
        private OperationTree ResolvTree_;

        public CalculationFormulaExecuter()
        {
            ResolvTree_ = new OperationTree();
            FourArismeticOperation =
                new Dictionary<string, Func<string, string, decimal>>
            {
                { @"*", (string left, string right)=>{ return decimal.Parse(left) * decimal.Parse(right); } },
                { @"/", (string left, string right)=>{ return decimal.Parse(left) / decimal.Parse(right); } },
                { @"+", (string left, string right)=>{ return decimal.Parse(left) + decimal.Parse(right); } },
                { @"-", (string left, string right)=>{ return decimal.Parse(left) - decimal.Parse(right); } },
                { @"?", (string left, string right)=>{ return (decimal.Parse(left) >= decimal.Parse(right)) ? 1 : 0; } },
                { @"\", (string left, string right)=>{ return (decimal.Parse(left) <= decimal.Parse(right)) ? 1 : 0; } },
                { @"<", (string left, string right)=>{ return (decimal.Parse(left) < decimal.Parse(right)) ? 1 : 0; } },
                { @">", (string left, string right)=>{ return (decimal.Parse(left) > decimal.Parse(right)) ? 1 : 0; } },
                { @"&", (string left, string right)=>{ return (decimal.Parse(left) != 0 & decimal.Parse(right) != 0) ? 1 : 0; } },
                { @"|", (string left, string right)=>{ return (decimal.Parse(left) != 0 | decimal.Parse(right) != 0) ? 1 : 0; } },
                { @"=", (string left, string right)=>{ return (decimal.Parse(left) == decimal.Parse(right)) ? 1 : 0; } },
                { @"!", (string left, string right)=>{ return (decimal.Parse(left) != decimal.Parse(right)) ? 1 : 0; } },
            };
        }

        public string DoesCalculate(in string targetFormula)
        {
            Stack<string> calculationStack = new Stack<string>();
            List<string> resolvedFormula = new List<string>();
            string returnString = string.Empty;

            ResolvTree_.TargetFormula = targetFormula;
            if (!ResolvTree_.ValidatesNest(targetFormula))
            {
                return ERR_MSG;
            }

            if (!ResolvTree_.GrowthTree())
            {
                return targetFormula;
            }

            ResolvTree_.GetTraverseStack(ref resolvedFormula);

            foreach (string value in resolvedFormula)
            {
                if (!FourArismeticOperation.ContainsKey(value))
                {
                    calculationStack.Push(value);
                }
                else if (calculationStack.Count > 1)
                {
                    string right = calculationStack.Pop();
                    string left = calculationStack.Pop();
                    if (!decimal.TryParse(right, out decimal resultRight))
                    {
                        calculationStack.Push(right);
                    }
                    else if (!decimal.TryParse(left, out decimal resultLeft))
                    {
                        calculationStack.Push(left);
                    }
                    else
                    {
                        calculationStack.Push(FourArismeticOperation[value](left, right).ToString());
                    }
                }
                else if (calculationStack.Count == 1)
                {
                    if (value == "-")
                    {
                        string right = calculationStack.Pop();
                        calculationStack.Push("-" + right);
                    }
                }
                else
                {
                    return ERR_MSG;
                }
            }
            return calculationStack.Pop();
        }
    }
}
