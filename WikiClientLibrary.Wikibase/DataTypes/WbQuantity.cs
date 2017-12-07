using System;

namespace WikiClientLibrary.Wikibase.DataTypes
{

    /// <summary>
    /// Represents an amount with arbitary precision, combined with a unit.
    /// </summary>
    public struct WbQuantity
    {

        /// <summary>
        /// The URI indicating the quantity has no unit (unity unit).
        /// </summary>
        public static Uri Unity { get; } = new Uri("1", UriKind.Relative);

        /// <inheritdoc cref="WbQuantity(double,double,double,Uri)"/>
        /// <summary>
        /// Initializes a new <see cref="WbQuantity"/> with the accurate amount, and entity URI of the unit.
        /// </summary>
        public WbQuantity(double amount, Uri unit) : this(amount, amount, amount, unit)
        {
            
        }

        /// <inheritdoc cref="WbQuantity(double,double,double,Uri)"/>
        /// <param name="error">The numberic error of the <paramref name="amount"/>.</param>
        public WbQuantity(double amount, double error, Uri unit) : this(amount, amount - error, amount + error, unit)
        {

        }

        /// <summary>
        /// Initializes a new <see cref="WbQuantity"/> with the amount, error, and entity URI of the unit.
        /// </summary>
        /// <param name="amount">The numeric value of the amount.</param>
        /// <param name="lowerBound">The lower-bound of the <paramref name="amount"/> caused by error.</param>
        /// <param name="upperBound">The upper-bound of the <paramref name="amount"/> caused by error.</param>
        /// <param name="unit">Entity URI of the unit. Use <see cref="Unity"/> for quantities that have no explict units.</param>
        /// <exception cref="ArgumentException"><paramref name="amount"/> is not in the range of <paramref name="lowerBound"/> and <paramref name="upperBound"/>.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="unit"/> is <c>null</c>.</exception>
        public WbQuantity(double amount, double lowerBound, double upperBound, Uri unit)
        {
            if (amount < lowerBound || amount > upperBound) throw new ArgumentException("amount should be between lowerBound and upperBound.");
            Amount = amount;
            LowerBound = lowerBound;
            UpperBound = upperBound;
            Unit = unit ?? throw new ArgumentNullException(nameof(unit));
        }

        // TODO Use more accurate decimal types.
        /// <summary>
        /// The numeric value of the amount.
        /// </summary>
        public double Amount { get; }

        /// <summary>
        /// The lower-bound of the <see cref="Amount"/> caused by error.
        /// </summary>
        public double LowerBound { get; }

        /// <summary>
        /// The upper-bound of the <see cref="Amount"/> caused by error.
        /// </summary>
        public double UpperBound { get; }

        /// <summary>
        /// Entity URI of the unit.
        /// </summary>
        public Uri Unit { get; }

        /// <summary>
        /// Determines whether the <see cref="Amount"/> has error.
        /// </summary>
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
            if (Unit != null) s += "(" + Unit.LocalPath + ")";
            return s;
        }
    }
}
