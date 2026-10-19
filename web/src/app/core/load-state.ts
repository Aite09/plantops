import { ProblemDetails } from './problem-details';

/** The three states every server-backed view can be in, so templates handle all of them explicitly. */
export type LoadState<T> =
  | { status: 'loading' }
  | { status: 'loaded'; data: T }
  | { status: 'error'; problem: ProblemDetails };
