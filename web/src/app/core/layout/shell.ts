import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthStore } from '../auth/auth-store';

/** Frame around every signed-in page: top bar with navigation, "Report fault" and the user menu. */
@Component({
  selector: 'po-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatButtonModule, MatIconModule, MatMenuModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  readonly #auth = inject(AuthStore);

  protected readonly user = this.#auth.user;
  protected readonly isAdmin = computed(() => this.#auth.hasAnyRole(['Admin']));
  protected readonly roleLabel = computed(() => this.user()?.roles.join(', ') ?? '');
  // Starts with the visible text (name, role), so speech-input users can say what they see (WCAG 2.5.3).
  protected readonly accountLabel = computed(() => `${this.user()?.displayName ?? ''}, ${this.roleLabel()}, account menu`);

  protected logout(): void {
    this.#auth.logout();
  }
}
