import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { LoadState } from '../../../core/load-state';
import { problemMessage } from '../../../core/problem-details';
import { FaultExplanation } from '../work-order.models';

/** Nothing asked yet, or the usual three states of a request. */
export type ExplanationState = { status: 'idle' } | LoadState<FaultExplanation>;

const BUTTON_LABELS: Record<ExplanationState['status'], string> = {
  idle: 'Explain this fault',
  loading: 'Explaining…',
  loaded: 'Explain again',
  error: 'Try again',
};

/**
 * Presentational: the "Explain this fault" section. The page owns the request and passes its state in. The model's
 * answer is untrusted text, so it is only ever interpolated ({{ }}), which Angular escapes: never [innerHTML], never
 * a Markdown renderer. The button stays in place (disabledInteractive) so keyboard focus is never lost while waiting.
 * "Explain again" really asks the model again: answers are not cached.
 */
@Component({
  selector: 'po-fault-explanation',
  imports: [DatePipe, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './fault-explanation.html',
  styleUrl: './fault-explanation.scss',
})
export class FaultExplanationPanel {
  readonly state = input.required<ExplanationState>();
  readonly explain = output<void>();

  protected readonly loading = computed(() => this.state().status === 'loading');
  protected readonly buttonLabel = computed(() => BUTTON_LABELS[this.state().status]);
  // Pressing again cannot help once the API says the feature is switched off.
  protected readonly showButton = computed(() => {
    const state = this.state();
    return !(state.status === 'error' && state.problem.code === 'FaultExplanation.Disabled');
  });
  protected readonly errorMessage = computed(() => {
    const state = this.state();
    return state.status === 'error' ? problemMessage(state.problem) : '';
  });

  protected request(): void {
    if (!this.loading()) {
      this.explain.emit();
    }
  }
}
