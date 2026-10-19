import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { applyServerErrors, ProblemDetails, problemMessage } from '../../../core/problem-details';
import { PlantUnit, PRIORITIES, WorkOrderDetails, WorkOrderInput, WorkOrderPriority } from '../work-order.models';

export const TITLE_MAX = 200;
export const DESCRIPTION_MAX = 4000;

/**
 * Presentational form shared by "report a fault" and "edit". Typed reactive form: every control has a compile-time
 * type, so form.getRawValue() is checked against WorkOrderInput. Limits match the API's validators.
 */
@Component({
  selector: 'po-work-order-form',
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonToggleModule,
    MatButtonModule,
    MatIconModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './work-order-form.html',
  styleUrl: './work-order-form.scss',
})
export class WorkOrderForm {
  readonly mode = input<'create' | 'edit'>('create');
  readonly plantUnits = input<PlantUnit[]>([]);
  /** Existing work order when editing. */
  readonly initial = input<WorkOrderDetails | null>(null);
  readonly submitting = input(false);
  /** The API's error for the last submit, if any. Field errors are shown under their fields. */
  readonly problem = input<ProblemDetails | null>(null);

  readonly save = output<WorkOrderInput>();
  readonly cancelled = output<void>();

  protected readonly titleMax = TITLE_MAX;
  protected readonly descriptionMax = DESCRIPTION_MAX;
  protected readonly priorities = [...PRIORITIES].reverse();

  protected readonly form = inject(NonNullableFormBuilder).group({
    plantUnitId: ['', Validators.required],
    equipmentId: ['', Validators.required],
    title: ['', [Validators.required, Validators.maxLength(TITLE_MAX)]],
    description: ['', [Validators.required, Validators.maxLength(DESCRIPTION_MAX)]],
    priority: ['Medium' as WorkOrderPriority, Validators.required],
  });

  readonly #unitId = toSignal(this.form.controls.plantUnitId.valueChanges, { initialValue: '' });
  protected readonly equipmentOptions = computed(
    () => this.plantUnits().find((unit) => unit.id === this.#unitId())?.equipment ?? [],
  );
  protected readonly descriptionLength = toSignal(this.form.controls.description.valueChanges, { initialValue: '' });
  protected readonly formError = signal<string | null>(null);

  constructor() {
    // Equipment belongs to a unit, so picking another unit clears the equipment choice. Only when reporting:
    // in edit mode both are filled together from the saved work order and can't be changed.
    this.form.controls.plantUnitId.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => {
        if (this.mode() === 'create') {
          this.form.controls.equipmentId.reset('');
        }
      });

    // Editing: fill the form from the work order. Equipment cannot be changed after reporting.
    effect(() => {
      const workOrder = this.initial();
      if (!workOrder) return;
      this.form.setValue({
        plantUnitId: workOrder.plantUnitId,
        equipmentId: workOrder.equipmentId,
        title: workOrder.title,
        description: workOrder.description,
        priority: workOrder.priority,
      });
      this.form.controls.plantUnitId.disable();
      this.form.controls.equipmentId.disable();
    });

    effect(() => {
      const problem = this.problem();
      if (!problem) {
        this.formError.set(null);
        return;
      }
      const unmatched = applyServerErrors(this.form, problem);
      const hasFieldErrors = Object.keys(problem.errors ?? {}).length > unmatched.length;
      this.formError.set(unmatched.length > 0 ? unmatched.join(' ') : hasFieldErrors ? null : problemMessage(problem));
    });
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    this.save.emit({
      title: value.title.trim(),
      description: value.description.trim(),
      priority: value.priority,
      equipmentId: value.equipmentId,
    });
  }
}
