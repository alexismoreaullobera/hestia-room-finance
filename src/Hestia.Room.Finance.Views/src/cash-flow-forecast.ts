import type { CashFlowForecastOutput, DailyBalanceOutput, DebitOutput } from "./shared/outputs";
import { amount, card, element, formatDay, formatEuros, svg, type ViewDefinition } from "./shared/view";
import "./cash-flow-forecast.css";

// Margins of the drawing area, in pixels: the chart is drawn at the width of the view (one viewBox unit per
// pixel), so its texts keep their size on a wide screen instead of growing with it.
const HEIGHT = 240, LEFT = 72, RIGHT = 12, TOP = 12, BOTTOM = 24;
export const MIN_WIDTH = 320;

/** The end-of-day balance of every day, the zero line, the part below zero and the low point. */
export function chart(days: DailyBalanceOutput[], lowPoint: DailyBalanceOutput, availableWidth: number): SVGSVGElement {
  const width = Math.max(MIN_WIDTH, Math.floor(availableWidth));
  const plotWidth = width - LEFT - RIGHT;
  const plotHeight = HEIGHT - TOP - BOTTOM;
  const step = plotWidth / Math.max(1, days.length - 1);
  const last = days.length - 1;

  const balances = days.map(d => d.balance);
  const min = Math.min(0, ...balances);
  const max = Math.max(0, ...balances);
  const margin = (max - min) * 0.08 || 1;
  const low = min - margin, high = max + margin;
  const x = (index: number) => LEFT + index * step;
  const y = (balance: number) => TOP + ((high - balance) * plotHeight) / (high - low);
  const zeroY = y(0);
  const points = days.map((d, i) => `${x(i).toFixed(1)},${y(d.balance).toFixed(1)}`);
  const first = days[0] ?? lowPoint;
  const final = days[last] ?? lowPoint;

  const label = `Courbe du solde, de ${formatEuros(first.balance)} le ${formatDay(first.date)} `
    + `à ${formatEuros(final.balance)} le ${formatDay(final.date)}, `
    + `point bas ${formatEuros(lowPoint.balance)} le ${formatDay(lowPoint.date)}.`;

  const chartNode = svg("svg", { class: "chart", viewBox: `0 0 ${width} ${HEIGHT}`, role: "img", "aria-label": label },
    svg("defs", {}, svg("clipPath", { id: "below-zero" },
      svg("rect", { x: LEFT, y: zeroY, width: plotWidth, height: Math.max(0, HEIGHT - BOTTOM - zeroY) }))),
    // Highest and lowest balances, unless they are the zero line itself (labelled below).
    ...[max, min].filter(value => value !== 0).flatMap(value => [
      svg("line", { class: "grid", x1: LEFT, x2: width - RIGHT, y1: y(value), y2: y(value) }),
      svg("text", { class: "axis", x: LEFT - 6, y: y(value) + 4, "text-anchor": "end" }, formatEuros(value)),
    ]),
    // The area between the curve and zero, kept only below zero by the clip.
    svg("polygon", {
      class: "below", "clip-path": "url(#below-zero)",
      points: `${x(0).toFixed(1)},${zeroY.toFixed(1)} ${points.join(" ")} ${x(last).toFixed(1)},${zeroY.toFixed(1)}`,
    }),
    svg("line", { class: "zero", x1: LEFT, x2: width - RIGHT, y1: zeroY, y2: zeroY }),
    svg("text", { class: "axis", x: LEFT - 6, y: zeroY + 4, "text-anchor": "end" }, "0 €"),
    svg("polyline", { class: "curve", points: points.join(" ") }));

  days.forEach((d, i) => {
    // One date every week on the time axis, and the last day (a weekly date too close to it is skipped);
    // the first and last dates are anchored inside the frame, not cut at its edges.
    if ((i % 7 === 0 && last - i > 3) || i === last) {
      const anchor = i === 0 ? "start" : i === last ? "end" : "middle";
      chartNode.append(svg("text", { class: "axis", x: x(i), y: HEIGHT - 6, "text-anchor": anchor }, formatDay(d.date)));
    }
    // A transparent column per day: hovering it shows the balance of that day (native SVG tooltip).
    chartNode.append(svg("rect", { class: "day", x: x(i) - step / 2, y: TOP, width: step, height: plotHeight },
      svg("title", {}, `${formatDay(d.date)} : ${formatEuros(d.balance)}`)));
  });

  const lowIndex = days.findIndex(d => d.date === lowPoint.date);
  if (lowIndex >= 0) {
    chartNode.append(svg("circle", { class: "low", cx: x(lowIndex), cy: y(lowPoint.balance), r: 4 }));
  }
  return chartNode;
}

function summary(data: CashFlowForecastOutput): HTMLElement {
  if (!data.hasAlert) {
    return element("div", { className: "calm" },
      element("strong", { className: "positive", text: "Pas d'alerte : " }),
      "le solde ne passe pas sous zéro ce mois-ci.");
  }
  // The same reading as the text summary of the tool: computed once, by the server.
  const advice = data.startsOverdrawn
    ? "Le compte démarre déjà à découvert : les mouvements du mois ne le creusent pas davantage."
    : `Il faudrait démarrer le mois avec au moins ${formatEuros(data.cashBuffer)} pour l'éviter.`;
  return element("div", { className: "alert" },
    element("strong", { className: "negative", text: "Alerte : " }),
    `le compte passe sous zéro (point bas ${formatEuros(data.lowPoint.balance)} le ${formatDay(data.lowPoint.date)}). `
      + advice);
}

function debits(contributingDebits: DebitOutput[]): HTMLElement[] {
  if (contributingDebits.length === 0) {
    return [];
  }
  return [
    element("h2", { text: "Débits qui creusent le point bas" }),
    element("table", {},
      element("thead", {}, element("tr", {},
        element("th", { text: "Date" }),
        element("th", { text: "Ligne" }),
        element("th", { className: "amount", text: "Montant" }))),
      element("tbody", {}, ...contributingDebits.map(debit => element("tr", {},
        element("td", { text: formatDay(debit.date) }),
        element("td", { text: debit.name }),
        amount(debit.amount, "td"))))),
  ];
}

export const cashFlowForecast: ViewDefinition<CashFlowForecastOutput> = {
  name: "Prévisionnel de trésorerie",
  tool: "show_cash_flow_forecast",
  render: (data, content, width) => {
    content.append(
      summary(data),
      element("div", { className: "cards" },
        card("Solde de départ", amount(data.startingBalance), "au début du mois"),
        card("Point bas", amount(data.lowPoint.balance), `le ${formatDay(data.lowPoint.date)}`),
        card("Coussin nécessaire", amount(data.cashBuffer), "pour ne jamais passer sous zéro")),
      chart(data.days, data.lowPoint, width),
      ...debits(data.contributingDebits));
  },
};
