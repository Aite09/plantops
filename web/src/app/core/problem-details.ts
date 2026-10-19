import { HttpErrorResponse } from '@angular/common/http';
import { FormGroup } from '@angular/forms';

/** RFC 7807 problem response, as returned by every PlantOps API error. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  /** Stable machine-readable code, e.g. "WorkOrder.InvalidTransition". */
  code?: string;
  /** Per-field validation messages, keyed by the JSON field name. */
  errors?: Record<string, string[]>;
}

export function toProblem(error: unknown): ProblemDetails {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return { status: 0, title: 'Cannot reach the PlantOps server. Check your connection and try again.' };
    }
    if (error.error && typeof error.error === 'object') {
      return { status: error.status, ...(error.error as ProblemDetails) };
    }
    return { status: error.status, title: error.statusText || 'Request failed.' };
  }
  return { title: 'Something went wrong.' };
}

/** One sentence for the user: the specific detail when the API gave one, otherwise the title. */
export function problemMessage(problem: ProblemDetails): string {
  if (problem.status === 429) {
    return 'Too many attempts. Wait a minute and try again.';
  }
  return problem.detail ?? problem.title ?? 'Something went wrong.';
}

/**
 * Puts server-side validation messages onto the matching form controls, so they show under the right field.
 * Returns the messages that matched no control, for display above the form.
 */
export function applyServerErrors(form: FormGroup, problem: ProblemDetails): string[] {
  const unmatched: string[] = [];
  for (const [field, messages] of Object.entries(problem.errors ?? {})) {
    const control = form.get(field);
    if (control) {
      // All messages for the field at once (e.g. every password rule), so one round trip shows everything to fix.
      control.setErrors({ server: messages.join(' ') });
      control.markAsTouched();
    } else {
      unmatched.push(...messages);
    }
  }
  return unmatched;
}
