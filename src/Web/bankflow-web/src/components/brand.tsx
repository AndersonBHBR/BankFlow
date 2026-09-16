import Link from "next/link";

type BrandProps = {
  compact?: boolean;
};

export function Brand({ compact = false }: BrandProps) {
  return (
    <Link className="brand" href="/dashboard" aria-label="BankFlow — início">
      <span className="brand-mark" aria-hidden="true">
        <span />
        <span />
        <span />
      </span>
      {!compact && (
        <span className="brand-copy">
          <strong>BankFlow</strong>
          <small>Digital Banking Lab</small>
        </span>
      )}
    </Link>
  );
}
