import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../core/services/auth.service';
import { WorkerJobService } from './worker-job.service';

import { environment } from '../../../environments/environment';

interface NavItem {
  label: string;
  route: string;
  icon: string;
  isPlaceholder?: boolean;
}

@Component({
  selector: 'app-worker-shell',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatButtonModule,
    MatIconModule
  ],
  templateUrl: './worker-shell.component.html'
})
export class WorkerShellComponent implements OnInit {
  protected readonly authService = inject(AuthService);
  private readonly workerService = inject(WorkerJobService);
  private readonly router = inject(Router);

  isMobileMenuOpen = signal(false);
  userName = signal<string>('Worker');
  userEmail = signal<string>('');
  userAvatarUrl = signal<string | null>(null);
  unreadNotifCount = signal<number>(0);

  readonly navItems: NavItem[] = [
    { label: 'Dashboard', route: '/worker', icon: 'dashboard' },
    { label: 'Browse Jobs', route: '/worker/jobs', icon: 'work' },
    { label: 'My Applications', route: '/worker/applications', icon: 'assignment' },
    { label: 'My Assignments', route: '/worker/assignments', icon: 'assignment_turned_in' },
    { label: 'My Attendance', route: '/worker/attendance', icon: 'event_available' },
    { label: 'My Earnings', route: '/worker/earnings', icon: 'account_balance_wallet' },
    { label: 'Work History', route: '/worker/work-history', icon: 'history' },
    { label: 'Job Preferences', route: '/worker/preferences', icon: 'tune' },
    { label: 'Notifications', route: '/worker/notifications', icon: 'notifications' },
    { label: 'Settings', route: '/worker/settings', icon: 'settings' },
    { label: 'My Profile', route: '/worker/profile', icon: 'person' }
  ];

  ngOnInit(): void {
    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.userName.set(user.fullName || 'Worker');
        this.userEmail.set(user.email || '');
        if (user.profileImageUrl) {
          let url = user.profileImageUrl;
          if (url.startsWith('/')) {
            url = `${environment.apiUrl.replace(/\/api$/, '')}${url}`;
          }
          this.userAvatarUrl.set(url);
        } else {
          this.userAvatarUrl.set(null);
        }
      }
    });

    this.workerService.getUnreadNotificationCount().subscribe({
      next: (res) => this.unreadNotifCount.set(res.unreadCount),
      error: () => {}
    });
  }

  toggleMobileMenu(): void {
    this.isMobileMenuOpen.update(val => !val);
  }

  closeMobileMenu(): void {
    this.isMobileMenuOpen.set(false);
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/auth/login']);
  }
}
