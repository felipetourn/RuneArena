from . import LABELS

REJECT = "?"


def is_correct(true, pred):
    # A rejected scribble is the right answer for noise, and the noise class is a rejection.
    if true == "noise":
        return pred in ("noise", REJECT)
    return pred == true


def confusion(pairs, title):
    cols = LABELS + [REJECT]
    counts = {(t, p): 0 for t in LABELS for p in cols}
    for t, p in pairs:
        counts[(t, p)] += 1

    correct = sum(is_correct(t, p) for t, p in pairs)
    lines = [f"{title}: {correct}/{len(pairs)} ({100.0 * correct / max(len(pairs), 1):.1f}%)",
             "true \\ pred".ljust(12) + "".join(c[:9].rjust(10) for c in cols)]
    for t in LABELS:
        if any(counts[(t, p)] for p in cols):
            lines.append(t.ljust(12) + "".join(str(counts[(t, p)]).rjust(10) for p in cols))
    return "\n".join(lines)
