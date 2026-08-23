import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { AuthService } from '../../core/services/auth.service';

import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { LanguageService } from '../../core/services/language.service';

interface ModuleCard {
  titleKey: string;
  descriptionKey: string;
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
    TranslatePipe,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule
  ],
  templateUrl: './farmer-dashboard.component.html'
})
export class FarmerDashboardComponent implements OnInit {
  private readonly authService = inject(AuthService);
  readonly languageService = inject(LanguageService);

  userName = signal<string>('Farmer');

  readonly moduleCards: ModuleCard[] = [
    {
      titleKey: 'nav.profile',
      descriptionKey: 'farmer.dashboardSubtitle',
      icon: 'person',
      route: '/farmer/profile'
    },
    {
      titleKey: 'nav.jobPostings',
      descriptionKey: 'worker.dashboardSubtitle',
      icon: 'work',
      route: '/farmer/jobs'
    },
    {
      titleKey: 'nav.crops',
      descriptionKey: 'farmer.dashboardSubtitle',
      icon: 'eco',
      route: '/farmer/crops'
    },
    {
      titleKey: 'nav.machinery',
      descriptionKey: 'customer.dashboardSubtitle',
      icon: 'construction',
      route: '/farmer/machinery',
      isPlaceholder: true
    },
    {
      titleKey: 'nav.auctions',
      descriptionKey: 'farmer.dashboardSubtitle',
      icon: 'storefront',
      route: '/farmer/auctions'
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
