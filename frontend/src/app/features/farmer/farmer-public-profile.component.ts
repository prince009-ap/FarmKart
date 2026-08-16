import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { FarmerProfileService } from '../../core/services/farmer-profile.service';
import { FarmerPublicProfileResponse } from '../../core/models/farmer-profile.models';

@Component({
  selector: 'app-farmer-public-profile',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatChipsModule
  ],
  templateUrl: './farmer-public-profile.component.html'
})
export class FarmerPublicProfileComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly profileService = inject(FarmerProfileService);

  farmerId = signal<string>('');
  loading = signal<boolean>(true);
  error = signal<string | null>(null);
  profile = signal<FarmerPublicProfileResponse | null>(null);

  auctionFilter = signal<'All' | 'Live' | 'Upcoming' | 'Ended'>('All');
  machineryFilter = signal<'All' | 'Available' | 'Rented' | 'Unavailable'>('All');

  filteredAuctions = computed(() => {
    const prof = this.profile();
    if (!prof) return [];
    const filter = this.auctionFilter();
    if (filter === 'All') return prof.activeAuctions;
    return prof.activeAuctions.filter(a => a.status.toUpperCase() === filter.toUpperCase());
  });

  filteredMachinery = computed(() => {
    const prof = this.profile();
    if (!prof) return [];
    const filter = this.machineryFilter();
    if (filter === 'All') return prof.machinery;
    return prof.machinery.filter(m => m.availabilityStatus.toUpperCase() === filter.toUpperCase());
  });

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const id = params.get('farmerId');
      if (id) {
        this.farmerId.set(id);
        this.loadProfile(id);
      }
    });
  }

  loadProfile(id: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.profileService.getPublicProfile(id).subscribe({
      next: (data) => {
        this.profile.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.message || 'Failed to load farmer profile.');
      }
    });
  }

  getRolePrefix(): string {
    const url = this.router.url;
    return url.startsWith('/farmer') ? '/farmer' : '/customer';
  }

  getAuctionStatusClass(status: string): string {
    switch (status.toUpperCase()) {
      case 'LIVE': return 'bg-emerald-500/20 text-emerald-300 border-emerald-500/40';
      case 'UPCOMING': return 'bg-amber-500/20 text-amber-300 border-amber-500/40';
      case 'ENDED': return 'bg-slate-800 text-slate-400 border-slate-700';
      default: return 'bg-sky-500/20 text-sky-300 border-sky-500/40';
    }
  }

  getMachineryStatusClass(status: string): string {
    switch (status.toUpperCase()) {
      case 'AVAILABLE': return 'bg-emerald-500/20 text-emerald-300 border-emerald-500/40';
      case 'RENTED': return 'bg-amber-500/20 text-amber-300 border-amber-500/40';
      case 'UNAVAILABLE': return 'bg-rose-500/20 text-rose-300 border-rose-500/40';
      default: return 'bg-slate-800 text-slate-400 border-slate-700';
    }
  }

  getStarArray(rating: number): number[] {
    const full = Math.floor(rating);
    return Array(full).fill(0);
  }
}
