import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  ElementRef,
  input,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { FormControl, FormGroup, FormGroupDirective, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { UserSummary } from '../../users/users-api';
import { AvailableActions } from '../work-order-actions';
import { WorkOrderStatus } from '../work-order.models';

/**
 * Presentational: the buttons this user may use right now. Assigning and closing happen inline (no dialogs):
 * pick a technician, or type resolution notes, right where the button was.
 */
@Component({
  selector: 'po-work-order-action-bar',
  imports: [ReactiveFormsModule, RouterLink, MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './work-order-action-bar.html',
  styleUrl: './work-order-action-bar.scss',
})
export class WorkOrderActionBar {
  readonly workOrderId = input.required<string>();
  readonly status = input.required<WorkOrderStatus>();
  readonly assignedToUserId = input<string | null>(null);
  readonly actions = input.required<AvailableActions>();
  readonly technicians = input<UserSummary[]>([]);
  readonly busy = input<string | null>(null);

  readonly approve = output<void>();
  readonly assign = output<string>();
  readonly start = output<void>();
  readonly close = output<string>();

  // Each inline form is a FormGroup bound with [formGroup]: that directive is what turns the browser's native submit
  // into (ngSubmit). A bare <form> with only [formControl] inside would do a real page-reloading submit.
  protected readonly assignForm = new FormGroup({
    technicianId: new FormControl('', { nonNullable: true, validators: Validators.required }),
  });
  protected readonly closeForm = new FormGroup({
    notes: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(4000)] }),
  });
  protected readonly closing = signal(false);
  private readonly notesInput = viewChild<ElementRef<HTMLTextAreaElement>>('notesInput');
  private readonly assignFormDirective = viewChild<FormGroupDirective>('assignFormDirective');

  protected readonly hasActions = computed(() => Object.values(this.actions()).some(Boolean));
  // While the close form is open its button is hidden; don't leave an empty bar behind.
  protected readonly showBar = computed(() => {
    const a = this.actions();
    return a.approve || a.assign || a.start || a.edit || (a.close && !this.closing());
  });
  protected readonly candidates = computed(() =>
    this.technicians().filter((technician) => technician.id !== this.assignedToUserId()),
  );

  constructor() {
    // A new status or assignee means the last action went through: reset the inline forms. (Reassigning keeps the
    // status at Assigned, so the assignee must be tracked too.) Only those two inputs are tracked; the resets run
    // untracked. resetForm() also clears the directive's "submitted" flag, which Material uses to show errors, so
    // the empty field doesn't light up red after a successful assign.
    effect(() => {
      this.status();
      this.assignedToUserId();
      untracked(() => {
        this.closing.set(false);
        this.closeForm.reset();
        this.assignFormDirective()?.resetForm();
      });
    });

    // Opening the close form moves focus into it, so keyboard users land where they need to type.
    effect(() => this.notesInput()?.nativeElement.focus());
  }

  protected submitAssign(): void {
    if (this.assignForm.invalid) {
      this.assignForm.markAllAsTouched();
      return;
    }
    this.assign.emit(this.assignForm.getRawValue().technicianId);
  }

  protected submitClose(): void {
    const notes = this.closeForm.controls.notes;
    // Validators.required accepts whitespace, so blank notes are flagged here; other errors (too long) stay as they are.
    if (!notes.value.trim()) {
      notes.setErrors({ required: true });
    }
    if (notes.invalid) {
      notes.markAsTouched();
      return;
    }
    this.close.emit(notes.value.trim());
  }
}
