import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterOutlet, RouterLinkActive } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { MatMenuModule } from '@angular/material/menu';
import { AuthService } from '../../core/services/auth.service';

interface NavItem {
  label: string;
  route: string;
  icon: string;
  isPlaceholder?: boolean;
}

@Component({
  selector: 'app-farmer-shell',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatButtonModule,
    MatIconModule,
    MatDividerModule,
    MatMenuModule
  ],
  templateUrl: './farmer-shell.component.html'
})
export class FarmerShellComponent implements OnInit {
  protected readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  isMobileMenuOpen = signal(false);
  userName = signal<string>('Farmer');
  userEmail = signal<string>('');

  readonly navItems: NavItem[] = [
    { label: 'Dashboard', route: '/farmer', icon: 'dashboard' },
    { label: 'My Profile', route: '/farmer/profile', icon: 'person' },
    { label: 'Jobs', route: '/farmer/jobs', icon: 'work' },
    { label: 'My Crops', route: '/farmer/crops', icon: 'eco', isPlaceholder: true },
    { label: 'Machinery', route: '/farmer/machinery', icon: 'construction', isPlaceholder: true },
    { label: 'Marketplace', route: '/farmer/marketplace', icon: 'storefront', isPlaceholder: true },
    { label: 'Notifications', route: '/farmer/notifications', icon: 'notifications', isPlaceholder: true }
  ];

  ngOnInit(): void {
    // Read the current user's profile details from the cached user session
    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.userName.set(user.fullName || 'Farmer');
        this.userEmail.set(user.email || '');
      }
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
