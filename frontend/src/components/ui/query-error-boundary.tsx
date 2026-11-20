"use client";

import { Component, type ReactNode } from "react";

type Props = {
  children: ReactNode;
  fallback?: ReactNode;
};

type State = { error: Error | null };

export class QueryErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  render() {
    if (this.state.error) {
      return (
        this.props.fallback ?? (
          <p className="text-sm text-amber-700" role="alert">
            {this.state.error.message ||
              "Unable to load this section. Check API connectivity and session."}
          </p>
        )
      );
    }
    return this.props.children;
  }
}
