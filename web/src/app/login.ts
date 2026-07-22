import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from './auth.service';

/**
 * Gate screen shown when `AuthService.username()` is null (5.11). Login/register share one form;
 * register additionally asks for the shared invite code, since it's otherwise open to anyone who
 * finds the URL — see docs/devplans/5.11-web-api-cicd-aws-hosting.md.
 */
@Component({
  selector: 'app-login',
  imports: [FormsModule],
  template: `
    <div class="wrap">
      <div class="card">
        <h1>Ueq Content</h1>
        <p class="sub">{{ mode() === 'login' ? 'Sign in to continue' : 'Create an account' }}</p>

        @if (error()) { <p class="error">{{ error() }}</p> }

        <label>Username</label>
        <input [(ngModel)]="username" name="username" autocomplete="username" />

        <label>Password</label>
        <input type="password" [(ngModel)]="password" name="password"
               [attr.autocomplete]="mode() === 'login' ? 'current-password' : 'new-password'" />

        @if (mode() === 'register') {
          <label>Invite code</label>
          <input [(ngModel)]="inviteCode" name="inviteCode" />
        }

        <button class="primary" [disabled]="busy()" (click)="submit()">
          {{ busy() ? 'Please wait…' : (mode() === 'login' ? 'Sign In' : 'Create Account') }}
        </button>

        <button class="link" (click)="toggleMode()">
          {{ mode() === 'login' ? "Need an account? Register" : 'Have an account? Sign in' }}
        </button>
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; font-family: system-ui, sans-serif; }
    .wrap { min-height: 100vh; display: flex; align-items: center; justify-content: center; background: #fafafa; }
    .card { width: 320px; padding: 2rem; background: #fff; border: 1px solid #e3e3e3; border-radius: 8px; }
    h1 { margin: 0 0 0.25rem; font-size: 1.3rem; }
    .sub { margin: 0 0 1.25rem; color: #666; font-size: 0.9rem; }
    label { display: block; font-size: 0.8rem; color: #555; margin: 0.75rem 0 0.25rem; }
    input { width: 100%; box-sizing: border-box; padding: 0.5rem; border: 1px solid #ccc; border-radius: 4px; }
    button.primary { width: 100%; margin-top: 1.25rem; padding: 0.6rem; border: none; border-radius: 4px;
                      background: #1a73e8; color: #fff; cursor: pointer; font-size: 0.95rem; }
    button.primary:disabled { background: #9db8e8; cursor: default; }
    button.link { width: 100%; margin-top: 0.6rem; padding: 0.4rem; border: none; background: none;
                   color: #1a73e8; cursor: pointer; font-size: 0.85rem; }
    .error { background: #fdecea; color: #a61b1b; padding: 0.5rem 0.7rem; border-radius: 4px; font-size: 0.85rem; }
  `],
})
export class Login {
  private readonly auth = inject(AuthService);

  readonly mode = signal<'login' | 'register'>('login');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  username = '';
  password = '';
  inviteCode = '';

  toggleMode(): void {
    this.mode.set(this.mode() === 'login' ? 'register' : 'login');
    this.error.set(null);
  }

  submit(): void {
    if (!this.username || !this.password) {
      this.error.set('Username and password are required.');
      return;
    }
    this.busy.set(true);
    this.error.set(null);

    const req$ = this.mode() === 'login'
      ? this.auth.login(this.username, this.password)
      : this.auth.register(this.username, this.password, this.inviteCode);

    req$.subscribe({
      next: () => this.busy.set(false),
      error: (e) => {
        this.busy.set(false);
        this.error.set(
          e?.status === 401 ? 'Invalid credentials or invite code.' :
          e?.status === 409 ? 'That username is already taken.' :
          e?.status === 0 ? 'Cannot reach the API — is it running?' :
          'Something went wrong.',
        );
      },
    });
  }
}
