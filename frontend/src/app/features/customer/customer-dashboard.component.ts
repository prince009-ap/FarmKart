import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-customer-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, MatButtonModule, MatIconModule],
  templateUrl: './customer-dashboard.component.html'
})
export class CustomerDashboardComponent implements OnInit {
  private readonly authService = inject(AuthService);

  userName = signal<string>('Customer');

  ngOnInit(): void {
    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.userName.set(user.fullName || 'Customer');
      }
    });
  }
}
