import type { ParsedIntent, SupplierResultEvent } from "../types";
import { CABIN_NAMES } from "../types";

type StageState = "pending" | "active" | "done";

interface Props {
  stages: { intent: StageState; suppliers: StageState; ranking: StageState; explanation: StageState };
  intent: ParsedIntent | null;
  supplierResults: SupplierResultEvent[];
}

const STAGE_LABELS: Array<{ key: keyof Props["stages"]; label: string }> = [
  { key: "intent", label: "Intent" },
  { key: "suppliers", label: "Suppliers" },
  { key: "ranking", label: "Ranking" },
  { key: "explanation", label: "Explanation" },
];

export function PipelineStatus({ stages, intent, supplierResults }: Props) {
  return (
    <section className="pipeline">
      <ol className="pipeline-steps">
        {STAGE_LABELS.map(({ key, label }) => (
          <li key={key} className={`pipeline-step pipeline-step--${stages[key]}`}>
            <span className="pipeline-dot" />
            <span>{label}</span>
          </li>
        ))}
      </ol>

      {intent && (
        <div className="intent-summary">
          <strong>
            {intent.origin} → {intent.destination}
          </strong>
          <span>{intent.departureDate}</span>
          <span>
            {intent.travellers.adults} adult{intent.travellers.adults === 1 ? "" : "s"}
          </span>
          <span>{CABIN_NAMES[intent.cabin] ?? "Economy"}</span>
          {intent.preferences.avoidRedEyes && <span className="tag">no red-eyes</span>}
          {intent.preferences.seatPreference && <span className="tag">{intent.preferences.seatPreference} seat</span>}
        </div>
      )}

      {supplierResults.length > 0 && (
        <div className="supplier-chips">
          {supplierResults.map((r) => (
            <span
              key={r.supplierId}
              className={`chip ${r.succeeded ? "chip--ok" : "chip--fail"}`}
              title={r.error ?? undefined}
            >
              {r.supplierId} · {r.succeeded ? `${r.offerCount} offer${r.offerCount === 1 ? "" : "s"}` : "failed"} ·{" "}
              {Math.round(r.elapsedMs)}ms
            </span>
          ))}
        </div>
      )}
    </section>
  );
}
