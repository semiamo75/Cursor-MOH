#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;
#endregion

// This strategy is meant to be copied into the NinjaTrader 8 NinjaScript editor.

namespace NinjaTrader.NinjaScript.Strategies
{
    public class AdaptiveFuturesScalper : Strategy
    {
        private const string LongSignal = "AFS_Long";
        private const string ShortSignal = "AFS_Short";

        private EMA fastEma;
        private EMA slowEma;
        private ATR atrIndicator;
        private VWAP vwapIndicator;
        private VOL volumeIndicator;
        private SMA volumeSma;

        private double sessionStartCumProfit;
        private bool tradingHalted;
        private int consecutiveLosingTrades;

        private double activeStopPrice = double.NaN;
        private bool breakEvenArmed;

        [NinjaScriptProperty]
        [Range(3, 50)]
        [Display(Name = "Fast EMA Period", Order = 0, GroupName = "Signal")]
        public int FastEmaPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(5, 200)]
        [Display(Name = "Slow EMA Period", Order = 1, GroupName = "Signal")]
        public int SlowEmaPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(5, 50)]
        [Display(Name = "ATR Period", Order = 2, GroupName = "Signal")]
        public int AtrPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ATR Stop Multiplier", Order = 3, GroupName = "Risk")]
        public double AtrStopMultiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Risk / Reward", Order = 4, GroupName = "Risk")]
        public double RiskRewardRatio { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Instrument Volatility Factor", Order = 5, GroupName = "Risk")]
        public double InstrumentVolatilityFactor { get; set; }

        [NinjaScriptProperty]
        [Range(0, 40)]
        [Display(Name = "VWAP Offset (ticks)", Order = 6, GroupName = "Signal")]
        public int VwapOffsetTicks { get; set; }

        [NinjaScriptProperty]
        [Range(0.5, 3)]
        [Display(Name = "Volume Multiplier", Order = 7, GroupName = "Signal")]
        public double VolumeMultiplier { get; set; }

        [NinjaScriptProperty]
        [Range(10, 200)]
        [Display(Name = "Volume SMA Period", Order = 8, GroupName = "Signal")]
        public int VolumeSmaPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Min ATR Filter", Order = 9, GroupName = "Signal")]
        public double MinAtrFilter { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max ATR Filter", Order = 10, GroupName = "Signal")]
        public double MaxAtrFilter { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trading Window Start (HHmmss)", Order = 11, GroupName = "Trading Window")]
        public int SessionStart { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trading Window End (HHmmss)", Order = 12, GroupName = "Trading Window")]
        public int SessionEnd { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Daily Loss Limit ($)", Order = 13, GroupName = "Risk")]
        public double DailyLossLimit { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Daily Profit Lock ($)", Order = 14, GroupName = "Risk")]
        public double DailyProfitLock { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Consecutive Losses", Order = 15, GroupName = "Risk")]
        public int MaxConsecutiveLosses { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Auto Position Sizing", Order = 16, GroupName = "PositionSizing")]
        public bool AutoSize { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Risk Per Trade ($)", Order = 17, GroupName = "PositionSizing")]
        public double RiskPerTrade { get; set; }

        [NinjaScriptProperty]
        [Range(1, 20)]
        [Display(Name = "Fixed Quantity", Order = 18, GroupName = "PositionSizing")]
        public int FixedQuantity { get; set; }

        [NinjaScriptProperty]
        [Range(1, 20)]
        [Display(Name = "Max Contracts", Order = 19, GroupName = "PositionSizing")]
        public int MaxContracts { get; set; }

        [NinjaScriptProperty]
        [Range(2, 50)]
        [Display(Name = "Min Stop (ticks)", Order = 20, GroupName = "Risk")]
        public int MinStopTicks { get; set; }

        [NinjaScriptProperty]
        [Range(4, 150)]
        [Display(Name = "Max Stop (ticks)", Order = 21, GroupName = "Risk")]
        public int MaxStopTicks { get; set; }

        [NinjaScriptProperty]
        [Range(2, 150)]
        [Display(Name = "Max Target (ticks)", Order = 22, GroupName = "Risk")]
        public int MaxTargetTicks { get; set; }

        [NinjaScriptProperty]
        [Range(2, 150)]
        [Display(Name = "Min Target (ticks)", Order = 23, GroupName = "Risk")]
        public int MinTargetTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Longs", Order = 24, GroupName = "Bias")]
        public bool EnableLongs { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Shorts", Order = 25, GroupName = "Bias")]
        public bool EnableShorts { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable BreakEven", Order = 26, GroupName = "ActiveRisk")]
        public bool EnableBreakEven { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "BreakEven Trigger (ticks)", Order = 27, GroupName = "ActiveRisk")]
        public double BreakEvenTriggerTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "BreakEven Plus (ticks)", Order = 28, GroupName = "ActiveRisk")]
        public double BreakEvenPlusTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Trailing Stop", Order = 29, GroupName = "ActiveRisk")]
        public bool EnableTrailing { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail Trigger (ticks)", Order = 30, GroupName = "ActiveRisk")]
        public double TrailTriggerTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail Step (ticks)", Order = 31, GroupName = "ActiveRisk")]
        public double TrailStepTicks { get; set; }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "AdaptiveFuturesScalper";
                Calculate = Calculate.OnEachTick;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.UniqueEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 15;
                IsInstantiatedOnEachOptimizationIteration = false;
                BarsRequiredToTrade = 50;
                EnableLongs = true;
                EnableShorts = true;

                FastEmaPeriod = 8;
                SlowEmaPeriod = 34;
                AtrPeriod = 14;
                AtrStopMultiplier = 1.2;
                RiskRewardRatio = 1.4;
                InstrumentVolatilityFactor = 1.0;
                VwapOffsetTicks = 6;
                VolumeMultiplier = 1.15;
                VolumeSmaPeriod = 50;
                MinAtrFilter = 0.3;
                MaxAtrFilter = 4.0;
                SessionStart = 80000;
                SessionEnd = 150000;
                DailyLossLimit = 1500;
                DailyProfitLock = 4000;
                MaxConsecutiveLosses = 3;
                AutoSize = true;
                RiskPerTrade = 600;
                FixedQuantity = 1;
                MaxContracts = 4;
                MinStopTicks = 4;
                MaxStopTicks = 40;
                MinTargetTicks = 6;
                MaxTargetTicks = 60;
                EnableBreakEven = true;
                BreakEvenTriggerTicks = 8;
                BreakEvenPlusTicks = 1;
                EnableTrailing = true;
                TrailTriggerTicks = 12;
                TrailStepTicks = 4;
            }
            else if (State == State.Configure)
            {
                tradingHalted = false;
                consecutiveLosingTrades = 0;
                sessionStartCumProfit = 0;
                breakEvenArmed = false;
                activeStopPrice = double.NaN;
            }
            else if (State == State.DataLoaded)
            {
                fastEma = EMA(FastEmaPeriod);
                slowEma = EMA(SlowEmaPeriod);
                atrIndicator = ATR(AtrPeriod);
                vwapIndicator = VWAP();
                volumeIndicator = VOL();
                volumeSma = SMA(volumeIndicator, VolumeSmaPeriod);
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToTrade)
                return;

            if (Bars.IsFirstBarOfSession)
                ResetSessionStats();

            if (tradingHalted)
                return;

            if (!WithinTradingWindow())
                return;

            double atrValue = atrIndicator[0];
            if (double.IsNaN(atrValue))
                return;

            atrValue *= InstrumentVolatilityFactor;

            if (atrValue < MinAtrFilter || atrValue > MaxAtrFilter)
                return;

            double vwapValue = vwapIndicator[0];
            double priceDistanceTicks = Math.Abs(Close[0] - vwapValue) / TickSize;
            bool nearVwap = priceDistanceTicks <= VwapOffsetTicks;

            double avgVolume = volumeSma[0];
            bool volumeConfirmation = avgVolume > 0 && volumeIndicator[0] >= avgVolume * VolumeMultiplier;

            bool longSignal = EnableLongs &&
                              fastEma[0] > slowEma[0] &&
                              Close[0] > vwapValue &&
                              nearVwap &&
                              volumeConfirmation;

            bool shortSignal = EnableShorts &&
                               fastEma[0] < slowEma[0] &&
                               Close[0] < vwapValue &&
                               nearVwap &&
                               volumeConfirmation;

            if (Position.MarketPosition == MarketPosition.Flat)
            {
                breakEvenArmed = false;
                activeStopPrice = double.NaN;

                if (longSignal)
                    SubmitEntry(true, atrValue);
                else if (shortSignal)
                    SubmitEntry(false, atrValue);
            }
            else
            {
                ManageOpenRisk();
            }
        }

        private void SubmitEntry(bool isLong, double atrValue)
        {
            double stopTicksRaw = (atrValue / TickSize) * AtrStopMultiplier;
            int stopTicks = ClampTicks((int)Math.Round(stopTicksRaw));
            int targetTicks = ClampTargetTicks((int)Math.Round(stopTicks * RiskRewardRatio));

            int quantity = ComputeOrderQuantity(stopTicks);
            if (quantity <= 0)
            {
                Draw.TextFixed(this, "AFS_Status", "Position size is 0. Increase RiskPerTrade or lower stop.", TextPosition.BottomRight);
                return;
            }

            double stopPrice = isLong
                ? Close[0] - stopTicks * TickSize
                : Close[0] + stopTicks * TickSize;

            double targetPrice = isLong
                ? Close[0] + targetTicks * TickSize
                : Close[0] - targetTicks * TickSize;

            string signalName = isLong ? LongSignal : ShortSignal;

            SetStopLoss(signalName, CalculationMode.Price, stopPrice, false);
            SetProfitTarget(signalName, CalculationMode.Price, targetPrice);

            if (isLong)
                EnterLong(quantity, signalName);
            else
                EnterShort(quantity, signalName);

            activeStopPrice = stopPrice;
            breakEvenArmed = false;
        }

        private int ComputeOrderQuantity(int stopTicks)
        {
            if (!AutoSize)
                return Math.Min(FixedQuantity, MaxContracts);

            double riskPerContract = stopTicks * TickSize * Instrument.MasterInstrument.PointValue;
            if (riskPerContract <= 0)
                return 0;

            int contracts = (int)Math.Floor(RiskPerTrade / riskPerContract);
            contracts = Math.Max(1, contracts);
            contracts = Math.Min(contracts, MaxContracts);
            return contracts;
        }

        private void ManageOpenRisk()
        {
            if (Position.MarketPosition == MarketPosition.Flat)
                return;

            double unrealizedTicks = Position.MarketPosition == MarketPosition.Long
                ? (Close[0] - Position.AveragePrice) / TickSize
                : (Position.AveragePrice - Close[0]) / TickSize;

            if (EnableBreakEven && !breakEvenArmed && unrealizedTicks >= BreakEvenTriggerTicks)
            {
                double breakEvenPrice = Position.AveragePrice + (Position.MarketPosition == MarketPosition.Long
                    ? BreakEvenPlusTicks * TickSize
                    : -BreakEvenPlusTicks * TickSize);

                string signal = Position.MarketPosition == MarketPosition.Long ? LongSignal : ShortSignal;
                SetStopLoss(signal, CalculationMode.Price, breakEvenPrice, false);
                activeStopPrice = breakEvenPrice;
                breakEvenArmed = true;
            }

            if (EnableTrailing && unrealizedTicks >= TrailTriggerTicks)
            {
                double newStop = Position.MarketPosition == MarketPosition.Long
                    ? Close[0] - TrailStepTicks * TickSize
                    : Close[0] + TrailStepTicks * TickSize;

                if (Position.MarketPosition == MarketPosition.Long)
                {
                    if (double.IsNaN(activeStopPrice) || newStop > activeStopPrice + TickSize)
                    {
                        SetStopLoss(LongSignal, CalculationMode.Price, newStop, false);
                        activeStopPrice = newStop;
                    }
                }
                else
                {
                    if (double.IsNaN(activeStopPrice) || newStop < activeStopPrice - TickSize)
                    {
                        SetStopLoss(ShortSignal, CalculationMode.Price, newStop, false);
                        activeStopPrice = newStop;
                    }
                }
            }
        }

        protected override void OnExecutionUpdate(Cbi.Execution execution, Cbi.Order order, double price, int quantity, Cbi.MarketPosition marketPosition)
        {
            if (execution == null || order == null)
                return;

            if (order.OrderState != OrderState.Filled)
                return;

            double sessionPnL = GetSessionPnL();
            double realizedChange = execution.ProfitCurrency;

            if (!double.IsNaN(realizedChange) && !double.IsInfinity(realizedChange) && realizedChange != 0)
            {
                if (realizedChange < 0)
                    consecutiveLosingTrades++;
                else
                    consecutiveLosingTrades = 0;
            }

            if (sessionPnL <= -Math.Abs(DailyLossLimit) ||
                sessionPnL >= Math.Abs(DailyProfitLock) ||
                consecutiveLosingTrades >= MaxConsecutiveLosses)
            {
                tradingHalted = true;
                Draw.TextFixed(this, "AFS_Halt", $"Trading halted. Session PnL: {sessionPnL:C2}", TextPosition.TopLeft);
            }
        }

        private void ResetSessionStats()
        {
            sessionStartCumProfit = SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit;
            tradingHalted = false;
            consecutiveLosingTrades = 0;
            breakEvenArmed = false;
            activeStopPrice = double.NaN;
        }

        private bool WithinTradingWindow()
        {
            int currentTime = ToTime(Time[0]);
            return currentTime >= SessionStart && currentTime <= SessionEnd;
        }

        private double GetSessionPnL()
        {
            double cumulative = SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit;
            return cumulative - sessionStartCumProfit;
        }

        private int ClampTicks(int ticks)
        {
            return Math.Max(MinStopTicks, Math.Min(MaxStopTicks, ticks));
        }

        private int ClampTargetTicks(int ticks)
        {
            ticks = Math.Max(MinTargetTicks, ticks);
            return Math.Min(MaxTargetTicks, ticks);
        }
    }
}
