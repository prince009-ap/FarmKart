import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { AuthService } from '../../core/services/auth.service';

interface ModuleCard {
  title: string;
  description: string;
  icon: string;
  route: string;
  isPlaceholder?: boolean;
}

@Component({
  selector: 'app-farmer-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule
  ],
  templateUrl: './farmer-dashboard.component.html'
})
export class FarmerDashboardComponent implements OnInit {
  private readonly authService = inject(AuthService);

  userName = signal<string>('Farmer');

  readonly moduleCards: ModuleCard[] = [
    {
      title: 'My Profile',
      description: 'View and update your personal info, farm size, unit settings, and contact information.',
      icon: 'person',
      route: '/farmer/profile'
    },
    {
      title: 'Jobs & Labor',
      description: 'Create job postings, review worker applications, manage assignments, and hire local farm help.',
      icon: 'work',
      route: '/farmer/jobs',
      isPlaceholder: true
    },
    {
      title: 'My Crops',
      description: 'Manage crop inventory, list items for direct sale, or configure live market listings.',
      icon: 'eco',
      route: '/farmer/crops',
      isPlaceholder: true
    },
    {
      title: 'Machinery Rentals',
      description: 'Rent heavy machinery from other farmers, list your own tools, and track rental agreements.',
      icon: 'construction',
      route: '/farmer/machinery',
      isPlaceholder: true
    },
    {
      title: 'Crops Marketplace',
      description: 'Advertise produce, review orders, configure crop listings, and sell directly to consumers.',
      icon: 'storefront',
      route: '/farmer/marketplace',
      isPlaceholder: true
    }
  ];

  ngOnInit(): void {
    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.userName.set(user.fullName || 'Farmer');
      }
    });
  }
}
