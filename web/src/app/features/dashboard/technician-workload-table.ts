import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TechnicianWorkload } from './dashboard-api';

/** Presentational: what each technician holds, in name order, including those with nothing (who can take work). */
@Component({
  selector: 'po-technician-workload-table',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-wrap">
      <table>
        <caption class="visually-hidden">Work held by each technician</caption>
        <thead>
          <tr>
            <th scope="col">Technician</th>
            <th scope="col" class="num">Assigned</th>
            <th scope="col" class="num">In progress</th>
          </tr>
        </thead>
        <tbody>
          @for (technician of technicians(); track technician.userId) {
            <tr>
              <th scope="row">{{ technician.displayName }}</th>
              <td class="num" [class.zero]="technician.assigned === 0">{{ technician.assigned }}</td>
              <td class="num" [class.zero]="technician.inProgress === 0">{{ technician.inProgress }}</td>
            </tr>
          } @empty {
            <tr>
              <td colspan="3" class="zero">No technician accounts yet.</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styleUrl: './technician-workload-table.scss',
})
export class TechnicianWorkloadTable {
  readonly technicians = input.required<TechnicianWorkload[]>();
}
