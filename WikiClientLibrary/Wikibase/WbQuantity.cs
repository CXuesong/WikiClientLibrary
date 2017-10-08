using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace WikiClientLibrary.Wikibase
{
    public struct WbQuantity
    {

        public WbQuantity(double amount, WbUri unit) : this(amount, amount, amount, unit)
        {
            
        }

        public WbQuantity(double amount, double error, WbUri unit) : this(amount, amount - error, amount + error, unit)
        {

        }

        public WbQuantity(double amount, double lowerBound, double upperBound, WbUri unit)
        {
            if (amount < lowerBound || amount > upperBound) throw new ArgumentException("amount should be between lowerBound and upperBound.");
            Amount = amount;
            LowerBound = lowerBound;
            UpperBound = upperBound;
            Unit = unit ?? throw new ArgumentNullException(nameof(unit));
        }

        // TODO Use more accurate decimal types.
        public double Amount { get; }

        public double LowerBound { get; }
        
        public double UpperBound { get; }

        public WbUri Unit { get; }

        public bool HasError => Amount != LowerBound || Amount != UpperBound;

        /// <inheritdoc />
        public override string ToString()
        {
            var s = Amount.ToString();
            if (HasError)
            {
                var upper = UpperBound - Amount;
                var lower = Amount - LowerBound;
                if (upper - lower < 1e-14 || (upper - lower) / Amount < 1e-14)
                    s += "±" + upper;
                else
                    s += "+" + upper + "/-" + lower;
            }
            if (Unit != null) s += "(" + Unit + ")";
            return s;
        }
    }
}
