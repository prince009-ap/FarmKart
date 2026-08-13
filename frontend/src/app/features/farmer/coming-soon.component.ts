import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-coming-soon',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatIconModule,
    MatButtonModule
  ],
  template: `
    <div class="flex flex-col items-center justify-center py-16 px-4 max-w-2xl mx-auto text-center">
      <mat-card class="p-8 border border-slate-100 shadow-xl rounded-2xl w-full bg-white relative overflow-hidden animate-fk-rise">
        <div class="absolute inset-0 bg-[radial-gradient(circle_at_70%_20%,rgba(16,185,129,0.08),transparent_45%)] pointer-events-none"></div>
        
        <div class="w-16 h-16 mx-auto bg-emerald-50 text-emerald-600 rounded-full flex items-center justify-center mb-6 border border-emerald-100">
          <mat-icon class="scale-125">auto_awesome</mat-icon>
        </div>

        <h1 class="text-2xl md:text-3xl font-bold font-serif text-slate-900 mb-2">
          {{ moduleTitle() }}
        </h1>
        
        <p class="text-slate-500 font-semibold uppercase tracking-wider text-xs mb-4">
          Coming Soon
        </p>

        <p class="text-slate-600 mb-8 max-w-md mx-auto">
          We are currently working hard to bring this feature to the FarmKart platform.
          In the next phase, you will be able to manage this workflow seamlessly here.
        </p>

        <div class="flex flex-col sm:flex-row items-center justify-center gap-3">
          <a mat-flat-button color="primary" routerLink="/farmer" class="font-semibold px-6 py-1">
            <mat-icon class="mr-1">dashboard</mat-icon>
            Back to Dashboard
          </a>
        </div>
      </mat-card>
    </div>
  `
})
export class ComingSoonComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  
  moduleTitle = signal<string>('Feature Module');

  ngOnInit(): void {
    this.route.data.subscribe(data => {
      if (data['title']) {
        this.moduleTitle.set(data['title']);
      }
    });
  }
}
