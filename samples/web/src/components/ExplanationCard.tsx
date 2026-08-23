import { useState } from "react";
import type { Explanation } from "../types";

interface Props {
  explanation: Explanation;
}

/** Highlights {{TOKEN}} placeholders in the raw model output so the price-integrity boundary is visible, not just asserted. */
function renderRawWithHighlightedTokens(raw: string) {
  const parts = raw.split(/(\{\{[A-Za-z0-9_-]+\}\})/g);
  return parts.map((part, i) =>
    part.startsWith("{{") ? (
      <span className="token" key={i}>
        {part}
      </span>
    ) : (
      <span key={i}>{part}</span>
    ),
  );
}

export function ExplanationCard({ explanation }: Props) {
  const [showRaw, setShowRaw] = useState(false);

  return (
    <section className="explanation">
      <div className="explanation-header">
        <h3>Explanation</h3>
        <label className="switch">
          <input type="checkbox" checked={showRaw} onChange={(e) => setShowRaw(e.target.checked)} />
          Show model's raw output (tokens, no numbers)
        </label>
      </div>

      <p className={showRaw ? "explanation-text explanation-text--raw" : "explanation-text"}>
        {showRaw ? renderRawWithHighlightedTokens(explanation.raw) : explanation.text}
      </p>

      <p className={`integrity-note ${explanation.isClean ? "integrity-note--clean" : "integrity-note--dirty"}`}>
        {explanation.isClean
          ? "Every token resolved from the price store — nothing the model wrote reached this page as a raw number."
          : "Unresolved tokens or stray digits detected — see PriceReferenceStore / ExplanationPlaceholderRenderer."}
      </p>
    </section>
  );
}
